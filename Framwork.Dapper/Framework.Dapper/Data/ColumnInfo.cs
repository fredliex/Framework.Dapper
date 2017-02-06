using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Framework.Data.ModelWrapper;

namespace Framework.Data
{
    internal sealed class ColumnInfo
    {
        /// <summary>成員</summary>
        internal MemberInfo Member { get; set; }

        /// <summary>成員名稱</summary>
        internal string MemberName { get; set; }

        /// <summary>成員資料型別</summary>
        internal Type ValueType { get; set; }

        /// <summary>成員資料是否為集合</summary>
        internal bool IsEnumerableValue { get; set; } = false;

        /// <summary>對應的欄位名稱</summary>
        internal string ColumnName { get; set; }

        /// <summary>是否為鍵值</summary>
        internal bool IsKey { get; set; } = false;

        /// <summary>當設定為true時, 於update與delete時是否會檢查此欄位必須與資料庫一致</summary>
        internal bool IsConcurrencyCheck { get; set; } = false;

        /// <summary>查詢時針對字串欄位是否去掉右邊空白。此設定僅作用於查詢時。</summary>
        internal bool IsTrimRight { get; set; } = false;

        /// <summary>model類型為null時所對應的資料庫欄位值。若為沒對應則此屬性為null。此為雙向對應。</summary>
        internal object NullMapping { get; set; } = null;

        /// <summary>產生get值的emit。</summary>
        internal Action<ILGenerator> GenerateGetEmit { get; set; } = null;

        /// <summary>產生set值的emit。</summary>
        internal Action<ILGenerator> GenerateSetEmit { get; set; } = null;

        private Func<Expression, Expression> columnValueGetterExpression = null;
        internal Func<Expression, Expression> ColumnValueGetterExpression => columnValueGetterExpression ?? (columnValueGetterExpression = GenerateColumnValueGetterExpression());

        /*
        private Func<object, object> columnValueGetter = null;
        internal Func<object, object> ColumnValueGetter => columnValueGetter ?? (columnValueGetter = GenerateColumnValueGetter());
        */


        internal ColumnInfo() { }

        internal ColumnInfo(MemberInfo member, Type valueType, ColumnAttribute columnAttribute, bool? isStructModel)
        {
            Member = member;
            MemberName = member.Name;
            var memberType = member.MemberType;
            var field = memberType == MemberTypes.Field ? (FieldInfo)member : null;
            var property = field == null & memberType == MemberTypes.Property ? (PropertyInfo)member : null;
            ValueType = valueType ?? field?.FieldType ?? property?.PropertyType;
            IsEnumerableValue = InternalHelper.IsEnumerableParameter(ValueType);

            //如果沒定義ColumnAttribute 或是 ColumnAttribute.Name 是null或是空白代表於類型名稱同名
            if (columnAttribute == null)
            {
                ColumnName = MemberName;
            }
            else
            {
                //未設定欄位名稱或是空白的話, 以屬性名稱視為欄位名稱
                ColumnName = string.IsNullOrWhiteSpace(columnAttribute.Name) ? MemberName : columnAttribute.Name;
                var behavior = columnAttribute.Behavior;
                IsConcurrencyCheck = (behavior & ColumnBehavior.ConcurrencyCheck) != 0;
                IsKey = (behavior & ColumnBehavior.Key) != 0;
                IsTrimRight = (behavior & ColumnBehavior.TrimRight) != 0;
                //如果model屬性類型為可null的，則設定model屬性為null時資料庫對應的特定值
                if (!ValueType.IsValueType || Nullable.GetUnderlyingType(ValueType) != null) NullMapping = columnAttribute.NullMapping;
                //如果是同步檢核的欄位的話, 則判斷model屬性類型必須為DateTime或是Nullable<DateTime>
                if (IsConcurrencyCheck && ValueType != typeof(DateTime) && ValueType != typeof(DateTime?))
                    throw new InvalidOperationException("ColumnBehavior.ConcurrencyCheck只允許設定於類型為DateTime或Nullable<DateTime>的成員上。");
            }
            if (field != null)
            {
                GenerateGetEmit = il => il.Emit(OpCodes.Ldfld, field);
                GenerateSetEmit = il => il.Emit(OpCodes.Stfld, field);
            }
            else if (property != null)
            {
                var callOpCode = isStructModel.Value ? OpCodes.Call : OpCodes.Callvirt;
                var getMethod = property.GetGetMethod(true) ?? property.GetGetMethod(false);
                var setMethod = property.GetSetMethod(true) ?? property.GetSetMethod(false);
                GenerateGetEmit = il => il.EmitCall(callOpCode, getMethod, null);
                GenerateSetEmit = il => il.EmitCall(callOpCode, setMethod, null);
            }
        }


        private static MethodInfo methodConvertListNull = typeof(ColumnInfo).GetMethod(nameof(ConvertListNull));
        /// <summary>產生取得column value的Expression</summary>
        /// <returns></returns>
        private Func<Expression, Expression> GenerateColumnValueGetterExpression()
        {
            var getMemberValue = Member.MemberType == MemberTypes.Field ?
                new Func<Expression, Expression>(expModel => Expression.Field(expModel, (FieldInfo)Member)) :
                new Func<Expression, Expression>(expModel => Expression.Property(expModel, (PropertyInfo)Member));
            MethodInfo methodConvertEnum = null;
            MethodInfo methodConvertNull = null;

            if (IsEnumerableValue)
            {
                Type enumValueType;
                var type = ValueType;
                methodConvertEnum = ModelWrapper.EnumValueHelper.GetValuesGetterMethod(ValueType, out enumValueType);
                if (methodConvertEnum != null) type = enumValueType;
                if (!InternalHelper.IsNullType(type)) methodConvertNull = methodConvertListNull;
            }
            else
            {

            }



            return expModel =>
            {
                var expValue = getMemberValue(expModel);
                if (methodConvertEnum != null) expValue = Expression.Call(methodConvertEnum, expValue);
                if (methodConvertNull != null) expValue = Expression.Call(methodConvertNull, expValue);

                return expValue;
            };
            /*

            Type enumValueType;
            if(IsEnumerableValue)
            {
                var enumMapper = ModelWrapper.EnumValueHelper.GetValuesGetterMethod(ValueType, out enumValueType);


            }
            */
            /*
            var enumMapper = IsEnumerableValue ?
                ModelWrapper.EnumValueHelper.GetValuesGetterMethod(ValueType, out enumValueType) :
                ModelWrapper.EnumValueHelper.GetValueGetterMethod(ValueType, out enumValueType);
            */

        }

        private static IEnumerable<object> ConvertListNull(IEnumerable<object> list, object nullValue)
        {
            return list.Select(n => n ?? nullValue);
        }


        /*
        /// <summary>產生取得value的Func</summary>
        /// <returns></returns>
        private Func<object, object> GenerateColumnValueGetter()
        {
            Type enumValueType;
            var method = IsEnumerableValue ? 
                ModelWrapper.EnumValueHelper.GetValuesGetterMethod(ValueType, out enumValueType) : 
                ModelWrapper.EnumValueHelper.GetValueGetterMethod(ValueType, out enumValueType);

            if(method == null)
            {
                return NullMapping == null ?
                    new Func<object, object>(v => v) :
                    new Func<object, object>(v => v == NullMapping ? null : v);
            }
            else
            {

            }

            if (NullMapping == null)
                return v => v;
            else
                return v => v == NullMapping ? null : v;

            return method == null ? value : method.Invoke(null, new object[] { value });
        }
        */
    }
}
