using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

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
        internal string ColName { get; set; }

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

        internal ColumnInfo() { }

        internal ColumnInfo(MemberInfo member, Type valueType, ColumnAttribute columnAttribute, bool? isStructModel)
        {
            Member = member;
            MemberName = member.Name;
            var memberType = member.MemberType;
            var field = memberType == MemberTypes.Field ? (FieldInfo)member : null;
            var property = field == null & memberType == MemberTypes.Property ? (PropertyInfo)member : null;
            ValueType = valueType ?? field?.FieldType ?? property?.PropertyType;
            IsEnumerableValue = IsEnumerableType(ValueType);

            //如果沒定義ColumnAttribute 或是 ColumnAttribute.Name 是null或是空白代表於類型名稱同名
            if (columnAttribute == null)
            {
                ColName = MemberName;
            }
            else
            {
                //未設定欄位名稱或是空白的話, 以屬性名稱視為欄位名稱
                ColName = string.IsNullOrWhiteSpace(columnAttribute.Name) ? MemberName : columnAttribute.Name;
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

        internal static bool IsEnumerableType(Type type)
        {
            return type != null && type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}
