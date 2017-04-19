using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq.Expressions;
using static Framework.Data.ModelWrapper;

namespace Framework.Data
{
    internal sealed class ModelColumnInfo : ColumnInfo
    {
        /// <summary>成員</summary>
        internal MemberInfo Member { get; private set; }

        /// <summary>產生get值的emit。</summary>
        internal Action<ILGenerator> GenerateGetEmit { get; private set; }

        /// <summary>產生set值的emit。</summary>
        internal Action<ILGenerator> GenerateSetEmit { get; private set; }

        private ModelColumnInfo(PropertyInfo property, FieldInfo field, ColumnAttribute columnAttribute, bool isStructModel) : 
            base(property?.Name ?? field?.Name, property?.PropertyType ?? field?.FieldType, columnAttribute)
        {
            Member = (MemberInfo)property ?? field;
            if (field != null)
            {
                GenerateGetEmit = il => il.Emit(OpCodes.Ldfld, field);
                GenerateSetEmit = il => il.Emit(OpCodes.Stfld, field);
            }
            else if (property != null)
            {
                var callOpCode = isStructModel ? OpCodes.Call : OpCodes.Callvirt;
                var getMethod = property.GetGetMethod(true) ?? property.GetGetMethod(false);
                var setMethod = property.GetSetMethod(true) ?? property.GetSetMethod(false);
                GenerateGetEmit = il => il.EmitCall(callOpCode, getMethod, null);
                GenerateSetEmit = il => il.EmitCall(callOpCode, setMethod, null);
            }
        }

        #region 取得抓值的Expression
        private Expression GetGetterExpression(ParameterExpression expModel)
        {
            Expression expValue = Member.MemberType == MemberTypes.Field ? Expression.Field(expModel, (FieldInfo)Member) : Expression.Property(expModel, (PropertyInfo)Member);
            MethodInfo methodConvertEnum = null;
            var elemType = ElementType;
            if (EnumInfo != null)
            {
                var isNullableEnum = Nullable.GetUnderlyingType(elemType) != null;
                methodConvertEnum = EnumInfo.Metadata.GetConverter(isNullableEnum, IsMultiValue);
                elemType = isNullableEnum ? EnumInfo.Metadata.NullableValueType : EnumInfo.Metadata.UnderlyingValueType;
            }
            if (methodConvertEnum != null) expValue = Expression.Call(methodConvertEnum, expValue);
            if (!IsMultiValue && !elemType.IsClass) expValue = Expression.Convert(expValue, typeof(object));
            //判斷目前資料是否可為null，可以的話就處理NullMapping
            if (NullMapping != null && InternalHelper.IsNullType(elemType))
            {
                var expNullValue = Expression.Constant(NullMapping, typeof(object));
                expValue = IsMultiValue ? (Expression)Expression.Call(methodConvertListNull, expValue, expNullValue) : Expression.Coalesce(expValue, expNullValue);
            }
            return expValue;
        }

        private static MethodInfo methodConvertListNull = typeof(ModelColumnInfo).GetMethod(nameof(ConvertListNull), BindingFlags.Static | BindingFlags.NonPublic);
        private static List<object> ConvertListNull(IEnumerable<object> list, object nullValue) => list?.Select(n => n ?? nullValue).ToList();
        #endregion

        #region 解析model
        /// <summary>依照model解析欄位資訊</summary>
        /// <param name="modelType">model型別</param>
        /// <param name="isDataModel">是否有繼承IDataModel</param>
        /// <param name="isStructType">model是否為值類型</param>
        /// <returns></returns>
        internal static IEnumerable<ModelColumnInfo> Resolve(Type modelType, bool? isDataModel = null, bool? isStructModel = null)
        {
            if (!isDataModel.HasValue) isDataModel = typeof(IDataModel).IsAssignableFrom(modelType);
            if (!isStructModel.HasValue) isStructModel = modelType.IsValueType;

            IEnumerable<ModelColumnInfo> columns;
            if (isDataModel.Value)
            {
                //這邊只是為了建立一個空的匿名物件字典, key是member name
                var memberDict = Enumerable.Empty<int>()
                    .ToDictionary(x => (string)null, x => new { member = (MemberInfo)null, isPublic = true, isField = true, attr = (ColumnAttribute)null });
                Action<Type, bool, BindingFlags> fillMembers = (type, isField, bindingFlags) =>
                {
                    var isPublic = (bindingFlags & BindingFlags.Public) == BindingFlags.Public;
                    //屬性的話排除有IndexParameters的
                    var members = isField ? 
                        (IEnumerable<MemberInfo>)type.GetFields(bindingFlags) : 
                        type.GetProperties(bindingFlags).Where(p => p.GetIndexParameters().Length == 0);
                    foreach (var member in members)
                    {
                        //如果public field有NonColumnAttribute就不處理
                        if (isPublic && member.IsDefined(typeof(NonColumnAttribute), false)) continue;
                        var attr = member.GetAttribute<ColumnAttribute>(false);
                        //如果nonpublic field且沒NonColumnAttribute就不處理
                        if (!isPublic && attr == null) continue;
                        memberDict[member.Name] = new { member, isPublic, isField, attr };
                    }
                };
                //有實作IDataModel時為了處理member Attribute的Inherited問題, 所以要順著繼承鍊來處理
                var inheritLink = new List<Type>();
                for (var type = modelType; type != typeof(object); type = type.BaseType) inheritLink.Add(type);
                for (var inheritIndex = inheritLink.Count - 1; inheritIndex >= 0; inheritIndex--)
                {
                    var type = inheritLink[inheritIndex];
                    fillMembers(type, true, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
                    fillMembers(type, true, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic);
                    fillMembers(type, false, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
                    fillMembers(type, false, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic);
                }
                //依照MetadataToken排序, 相當於出現的順序
                columns = memberDict.OrderBy(n => n.Value.member.MetadataToken).Select(n => {
                    var isField = n.Value.isField;
                    return new ModelColumnInfo(isField ? null : (PropertyInfo)n.Value.member, isField ? (FieldInfo)n.Value.member : null, n.Value.attr, isStructModel.Value);
                });
            }
            else if (Reflect.Dapper.IsValueTuple(modelType))
            {
                columns = modelType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(f => new ModelColumnInfo(null, f, null, isStructModel.Value));
            }
            else
            {
                //沒實作IDataModel時, 只抓public屬性, 不需逐繼承鍊處理
                columns = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.GetIndexParameters().Length == 0)
                    .Select(p => new ModelColumnInfo(p, null, p.GetAttribute<ColumnAttribute>(false), isStructModel.Value))
                    .OrderBy(n => n.Member.Name);
            }
            return SortColumn(modelType, columns);
        }

        //判斷是不是像tuple, 是的話就依照建構式排序, 否則依照原本名稱排序的方式
        private static List<ModelColumnInfo> SortColumn(Type modelType, IEnumerable<ModelColumnInfo> columns)
        {
            var listByName = columns.ToList();
            //如果建構式不只一個, 或是建構式的參數數量和欄位數不符, 就不改變排序
            var ctors = modelType.GetConstructors();
            if (ctors.Length != 1) return listByName;
            var ctorParams = ctors[0].GetParameters();
            if (listByName.Count != ctorParams.Length) return listByName;
            //如果建構式只有一個, 且參數與欄位名稱相符, 就改用參數的順序回傳, 否則就傳回原順序
            var dict = listByName.ToDictionary(n => n.MemberName, StringComparer.OrdinalIgnoreCase);
            var listByCtor = new List<ModelColumnInfo>(ctorParams.Length);
            ModelColumnInfo tmpInfo;
            for (var i = 0; i < ctorParams.Length; i++)
            {
                if (!dict.TryGetValue(ctorParams[i].Name, out tmpInfo)) return listByName;
                listByCtor.Add(tmpInfo);
            }
            return listByCtor;
        }
        #endregion

        #region 將Model轉成Dictionary
        private static MethodInfo methodDictionaryItemSet = typeof(IDictionary<string, object>).GetProperties().First(p => p.GetIndexParameters().Length > 0).GetSetMethod();
        internal static Action<IDictionary<string, object>, object> GenerateDictionaryFiller(Type modelType, IEnumerable<ModelColumnInfo> columns)
        {
            var expParamDict = Expression.Parameter(typeof(IDictionary<string, object>));
            var expParamObject = Expression.Parameter(typeof(object));
            var expVarModel = Expression.Variable(modelType);
            var expBody = new List<Expression>();
            expBody.Add(Expression.Assign(expVarModel, Expression.Convert(expParamObject, modelType)));
            expBody.AddRange(columns.Select(col => Expression.Call(expParamDict, methodDictionaryItemSet, Expression.Constant(col.ColumnName), col.GetGetterExpression(expVarModel))));
            var expBlock = Expression.Block(new[] { expVarModel }, expBody);
            var lambda = Expression.Lambda<Action<IDictionary<string, object>, object>>(expBlock, new[] { expParamDict, expParamObject });
            return lambda.Compile();
        }

        //這邊用ModelTableInfo.Get是為了cache, 不然其實直接用 Resolve 就足夠了
        internal static Action<IDictionary<string, object>, object> GenerateDictionaryFiller(Type modelType) =>
            GenerateDictionaryFiller(modelType, ModelTableInfo.Get(modelType).Columns);


        #endregion
    }
}
