using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class ModelWrapper
    {
        private static class EnumValueHelper
        {
            private abstract class EnumHandlerBase : Dapper.SqlMapper.ITypeHandler
            {
                internal readonly Type EnumType;
                internal readonly Type ValueType;
                internal readonly Type ValueUnderlyingType;
                //public readonly MethodInfo ValueGetterMethod;
                internal readonly MethodInfo enumToValue;    //enum 轉 value
                internal readonly MethodInfo enumsToValues;  //enums 轉 values
                internal readonly MethodInfo nullEnumToValue;  //nullable<enum> 轉 value
                internal readonly MethodInfo nullEnumsToValues; //nullable<enum>s 轉 values

                /*
                internal MethodInfo GetValueGetterMethod(bool isNullableEnum)
                {
                    return isNullableEnum ? nullEnumToValue : enumToValue;
                }
                internal MethodInfo GetValuesGetterMethod(bool isNullableEnum)
                {
                    return isNullableEnum ? nullEnumsToValues : enumsToValues;
                }
                */

                protected EnumHandlerBase(Type enumType, Type valueUnderlyingType, Type enumCacheType)
                {
                    EnumType = enumType;
                    ValueUnderlyingType = valueUnderlyingType;
                    if (valueUnderlyingType.IsValueType)
                    {
                        ValueType = typeof(Nullable<>).MakeGenericType(valueUnderlyingType);
                        enumToValue = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.EnumToStruct)).MakeGenericMethod(valueUnderlyingType);
                        enumsToValues = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.EnumsToStructValues)).MakeGenericMethod(valueUnderlyingType);
                        nullEnumToValue = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.NullEnumToStruct)).MakeGenericMethod(valueUnderlyingType);
                        nullEnumsToValues = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.NullEnumsToStructValues)).MakeGenericMethod(valueUnderlyingType);
                    }
                    else
                    {
                        ValueType = valueUnderlyingType;
                        enumToValue = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.EnumToClass)).MakeGenericMethod(valueUnderlyingType);
                        enumsToValues = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.EnumsToClassValues)).MakeGenericMethod(valueUnderlyingType);
                        nullEnumToValue = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.NullEnumToClass)).MakeGenericMethod(valueUnderlyingType);
                        nullEnumsToValues = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.NullEnumsToClassValues)).MakeGenericMethod(valueUnderlyingType);
                    }
                }

                public abstract void SetValue(IDbDataParameter parameter, object value);
                public abstract object Parse(Type destinationType, object value);
            }

            private sealed class EnumHandler<TEnum, TValue> : EnumHandlerBase where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                /// <summary>Enum轉資料庫欄位值的對應</summary>
                private readonly ReadOnlyDictionary<TEnum, TValue> toValueMap;
                /// <summary>資料庫欄位值轉Enum的對應</summary>
                private readonly ReadOnlyDictionary<TValue, TEnum> toEnumMap;
                /// <summary>當TValue為null時對應的Enum，null表示沒對應。</summary>
                private readonly TEnum? nullValue;

                public EnumHandler(IList enums, IList values, object nullValue) : base(typeof(TEnum), typeof(TValue), typeof(EnumHandlerCache<TEnum>))
                {
                    var tmpMapDict = new Dictionary<TEnum, TValue>(enums.Count);
                    var tmpEnumDict = new Dictionary<TValue, TEnum>(enums.Count);
                    for (var i = 0; i < enums.Count; i++)
                    {
                        var enumValue = (TEnum)enums[i];
                        var mapValue = (TValue)values[i];
                        tmpMapDict[enumValue] = mapValue;
                        tmpEnumDict[mapValue] = enumValue;
                    }
                    this.nullValue = (TEnum?)nullValue;
                    toValueMap = new ReadOnlyDictionary<TEnum, TValue>(tmpMapDict);
                    toEnumMap = new ReadOnlyDictionary<TValue, TEnum>(tmpEnumDict);

                    EnumHandlerCache<TEnum>.Handler = this;
                }

                internal bool TryGetValue(TEnum vEnum, out TValue vValue, out bool isNullValue)
                {
                    isNullValue = false;
                    vValue = default(TValue);
                    if (nullValue.HasValue && EqualityComparer<TEnum>.Default.Equals(nullValue.GetValueOrDefault(), vEnum))
                    {
                        isNullValue = true;
                        return true;
                    }
                    return toValueMap.TryGetValue(vEnum, out vValue);
                }

                public override object Parse(Type destinationType, object value)
                {
                    throw new NotImplementedException();
                }

                public override void SetValue(IDbDataParameter parameter, object vEnum)
                {
                    TValue value;
                    bool isNull;
                    if (!TryGetValue((TEnum)vEnum, out value, out isNull)) throw new Exception($"未定義{vEnum}的對應值");
                    parameter.Value = isNull ? (object)DBNull.Value : value;
                }
            }


            /*
            private static class EnumMapCache<TEnum, TValue> where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                /// <summary>Enum轉資料庫欄位值的對應</summary>
                private static ReadOnlyDictionary<TEnum, TValue> toValueMap;
                /// <summary>資料庫欄位值轉Enum的對應</summary>
                private static ReadOnlyDictionary<TValue, TEnum> toEnumMap;
                /// <summary>當TValue為null時對應的Enum，null表示沒對應。</summary>
                private static TEnum? nullValue;

                public static void Init(IList enums, IList values, TEnum? nullMap)
                {

                    var tmpMapDict = new Dictionary<TEnum, TValue>(enums.Count);
                    var tmpEnumDict = new Dictionary<TValue, TEnum>(enums.Count);
                    for (var i = 0; i < enums.Count; i++)
                    {
                        var enumValue = (TEnum)enums[i];
                        var mapValue = (TValue)values[i];
                        tmpMapDict[enumValue] = mapValue;
                        tmpEnumDict[mapValue] = enumValue;
                    }
                    nullValue = nullMap;
                    toValueMap = new ReadOnlyDictionary<TEnum, TValue>(tmpMapDict);
                    toEnumMap = new ReadOnlyDictionary<TValue, TEnum>(tmpEnumDict);
                }

                internal static bool TryGetValue(TEnum vEnum, out TValue vValue, out bool isNullValue)
                {
                    isNullValue = false;
                    vValue = default(TValue);
                    if (nullValue.HasValue && EqualityComparer<TEnum>.Default.Equals(nullValue.GetValueOrDefault(), vEnum))
                    {
                        isNullValue = true;
                        return true;
                    }
                    return toValueMap.TryGetValue(vEnum, out vValue);
                }
            }
            */

            /*
            private sealed class Info
            {
                public Type EnumType { get; }
                public Type ValueType { get; }
                public Type ValueUnderlyingType { get; }
                internal Info(Type enumType, Type valueUnderlyingType)
                {
                    EnumType = enumType;
                    ValueUnderlyingType = valueUnderlyingType;
                    ValueType = valueUnderlyingType.IsValueType ? typeof(Nullable<>).MakeGenericType(valueUnderlyingType) : valueUnderlyingType;
                }
                internal bool IsStructValue()
                {
                    return ValueType != ValueUnderlyingType;  //如果是物件類型的話, ValueType會等於ValueUnderlyingType
                }
            }
            */

            private static class EnumHandlerCache<TEnum> where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                internal static EnumHandlerBase Handler;

                #region 單個
                public static TValue EnumToClass<TValue>(TEnum enumValue) where TValue : class
                {
                    var handler = (EnumHandler<TEnum, TValue>)Handler;
                    TValue value;
                    bool isNull;
                    if (handler.TryGetValue(enumValue, out value, out isNull)) return isNull ? null : value;
                    throw new Exception($"未定義{enumValue}的對應值");
                }

                public static TValue NullEnumToClass<TValue>(TEnum? enumValue) where TValue : class
                {
                    return enumValue.HasValue ? EnumToClass<TValue>(enumValue.GetValueOrDefault()) : null;
                }

                public static TValue? EnumToStruct<TValue>(TEnum enumValue) where TValue : struct
                {
                    var handler = (EnumHandler<TEnum, TValue>)Handler;
                    TValue value;
                    bool isNull;
                    if (handler.TryGetValue(enumValue, out value, out isNull)) return isNull ? (TValue?)null : value;
                    throw new Exception($"未定義{enumValue}的對應值");
                }

                public static TValue? NullEnumToStruct<TValue>(TEnum? enumValue) where TValue : struct
                {
                    return enumValue.HasValue ? EnumToStruct<TValue>(enumValue.GetValueOrDefault()) : (TValue?)null;
                }
                #endregion

                #region 集合
                public static IEnumerable<TValue> EnumsToClassValues<TValue>(IEnumerable<TEnum> enumValues) where TValue : class
                {
                    return enumValues.Select(EnumToClass<TValue>);
                }

                public static IEnumerable<TValue> NullEnumsToClassValues<TValue>(IEnumerable<TEnum?> enumValues) where TValue : class
                {
                    return enumValues.Select(NullEnumToClass<TValue>);
                }

                public static IEnumerable<TValue?> EnumsToStructValues<TValue>(IEnumerable<TEnum> enumValues) where TValue : struct
                {
                    return enumValues.Select(EnumToStruct<TValue>);
                }

                public static IEnumerable<TValue?> NullEnumsToStructValues<TValue>(IEnumerable<TEnum?> enumValues) where TValue : struct
                {
                    return enumValues.Select(NullEnumToStruct<TValue>);
                }
                #endregion
            }

            /*
            private static readonly MethodInfo enumToStruct = typeof(EnumValueHelper).GetMethod(nameof(EnumToStruct));
            private static readonly MethodInfo enumToClass = typeof(EnumValueHelper).GetMethod(nameof(EnumToClass));
            private static readonly MethodInfo nullEnumToStruct = typeof(EnumValueHelper).GetMethod(nameof(NullEnumToStruct));
            private static readonly MethodInfo nullEnumToClass = typeof(EnumValueHelper).GetMethod(nameof(NullEnumToClass));

            private static readonly MethodInfo enumsToStructValues = typeof(EnumValueHelper).GetMethod(nameof(EnumsToStructValues));
            private static readonly MethodInfo enumsToClassValues = typeof(EnumValueHelper).GetMethod(nameof(EnumsToClassValues));
            private static readonly MethodInfo nullEnumsToStructValues = typeof(EnumValueHelper).GetMethod(nameof(NullEnumsToStructValues));
            private static readonly MethodInfo nullEnumsToClassValues = typeof(EnumValueHelper).GetMethod(nameof(NullEnumsToClassValues));
            */

            private static readonly ConcurrentDictionary<Type, EnumHandlerBase> handlers = new ConcurrentDictionary<Type, EnumHandlerBase>();



            private static EnumHandlerBase CreateHandler(Type enumType)
            {
                var fields = enumType.GetFields(BindingFlags.Static | BindingFlags.Public);
                var enums = new ArrayList(fields.Length);
                var values = new ArrayList(fields.Length);
                Type valueType = null;
                object nullEnum = null;     //資料庫為null時對應的enum

                #region 取得成員值以及對應值
                foreach (FieldInfo field in fields)
                {
                    var attr = field.GetAttribute<ValueAttribute>(false);
                    if (attr == null) continue;
                    var dbValue = attr.Value;
                    var enumValue = field.GetValue(null);
                    if (dbValue == null)
                    {
                        if (nullEnum != null) throw new Exception("不可同時多個ValueAttribute都定義為null。");
                        nullEnum = enumValue;
                    }
                    else
                    {
                        var tmpDbType = dbValue.GetType();
                        if (tmpDbType == valueType)
                        {
                            //不需作任何特殊處理, 會有這判斷是因為預期Enum成員ValueAttribute的定義值都是同類型, 所以優先判斷相等
                        }
                        else if (valueType == null)
                        {
                            valueType = tmpDbType;
                        }
                        else
                        {
                            throw new Exception("當Enum定義ValueAttribute時，ValueAttribute所設定的Value型別必須一致。");
                        }
                        values.Add(dbValue);
                        enums.Add(enumValue);
                    }
                }
                #endregion
                if (valueType == null) return null;
                if (enumType.IsDefined(typeof(FlagsAttribute))) throw new NotSupportedException("目前不支援有標示FlagsAttribute的列舉");

                //建立handler並註冊到Dapper同時回傳
                var handler = (EnumHandlerBase)typeof(EnumHandler<,>).MakeGenericType(enumType, valueType).GetConstructor(new[] { typeof(IList), typeof(IList), typeof(object) }).Invoke(new object[] { enums, values, nullEnum });
                Dapper.SqlMapper.AddTypeHandler(enumType, handler);
                return handler;

                //typeof(EnumMapCache<,>).MakeGenericType(enumType, valueType).GetMethod(nameof(EnumMapCache<int, int>.Init)).Invoke(null, new object[] { enums, values, nullEnum });
                //return new Info(enumType, valueType);
            }

            private static EnumHandlerBase GetHandler(Type memberType, out bool isNullableEnum)
            {
                isNullableEnum = false;
                if (memberType.IsValueType)
                {
                    var nullType = Nullable.GetUnderlyingType(memberType);
                    var enumType = nullType ?? memberType;
                    isNullableEnum = nullType != null;
                    if (enumType.IsEnum) return handlers.GetOrAdd(enumType, CreateHandler);
                }
                return null;
            }

            internal static MethodInfo GetValueGetterMethod(Type memberType, out Type valueType)
            {
                valueType = null;
                bool isNullableEnum;
                var handler = GetHandler(memberType, out isNullableEnum);
                if (handler == null) return null;
                valueType = handler.ValueType;
                return isNullableEnum ? handler.nullEnumToValue : handler.enumToValue;
            }


            internal static MethodInfo GetValuesGetterMethod(Type memberType, out Type valueType)
            {
                valueType = null;
                var enumerableType = memberType.GetInterfaces().FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (enumerableType == null) return null;
                var elemType = enumerableType.GetGenericArguments()[0];
                bool isNullableEnum;
                var handler = GetHandler(elemType, out isNullableEnum);
                if (handler == null) return null;
                valueType = handler.ValueType;
                return isNullableEnum ? handler.nullEnumsToValues : handler.enumsToValues;
            }

            /*
            #region 單個
            public static TValue EnumToClass<TEnum, TValue>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : class
            {
                TValue value;
                bool isNull;
                if (EnumMapCache<TEnum, TValue>.TryGetValue(enumValue, out value, out isNull)) return isNull ? null : value;
                throw new Exception($"未定義{enumValue}的對應值");
            }

            public static TValue NullEnumToClass<TEnum, TValue>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : class
            {
                return enumValue.HasValue ? EnumToClass<TEnum, TValue>(enumValue.GetValueOrDefault()) : null;
            }

            public static TValue? EnumToStruct<TEnum, TValue>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : struct
            {
                TValue value;
                bool isNull;
                if (EnumMapCache<TEnum, TValue>.TryGetValue(enumValue, out value, out isNull)) return isNull ? (TValue?)null : value;
                throw new Exception($"未定義{enumValue}的對應值");
            }

            public static TValue? NullEnumToStruct<TEnum, TValue>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : struct
            {
                return enumValue.HasValue ? EnumToStruct<TEnum, TValue>(enumValue.GetValueOrDefault()) : (TValue?)null;
            }
            #endregion
            */



            /*
            #region 集合
            public static IEnumerable<TValue> EnumsToClassValues<TEnum, TValue>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : class
            {
                return enumValues.Select(EnumToClass<TEnum, TValue>);
            }

            public static IEnumerable<TValue> NullEnumsToClassValues<TEnum, TValue>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : class
            {
                return enumValues.Select(NullEnumToClass<TEnum, TValue>);
            }

            public static IEnumerable<TValue?> EnumsToStructValues<TEnum, TValue>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : struct
            {
                return enumValues.Select(EnumToStruct<TEnum, TValue>);
            }

            public static IEnumerable<TValue?> NullEnumsToStructValues<TEnum, TValue>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where TValue : struct
            {
                return enumValues.Select(NullEnumToStruct<TEnum, TValue>);
            }
            #endregion
            */

        }
    }
}
