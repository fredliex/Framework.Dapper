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
        internal static class EnumValueHelper
        {
            internal abstract class EnumHandlerBase : Dapper.SqlMapper.ITypeHandler
            {
                internal readonly Type EnumType;
                internal readonly Type ValueType;
                internal readonly Type ValueUnderlyingType;
                internal readonly MethodInfo enumToValue;    //enum 轉 value
                internal readonly MethodInfo enumsToValues;  //enums 轉 values
                internal readonly MethodInfo nullEnumToValue;  //nullable<enum> 轉 value
                internal readonly MethodInfo nullEnumsToValues; //nullable<enum>s 轉 values
                internal readonly MethodInfo valueToEnum;   //value 轉 enum

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
                    valueToEnum = enumCacheType.GetMethod(nameof(EnumHandlerCache<int>.ValueToEnum));
                }

                public abstract void SetValue(IDbDataParameter parameter, object value);
                public abstract object Parse(Type destinationType, object value);

                public abstract object NullValue { get; }
            }

            private sealed class EnumHandler<TEnum, TValue> : EnumHandlerBase where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                /// <summary>Enum轉資料庫欄位值的對應</summary>
                private readonly ReadOnlyDictionary<TEnum, TValue> toValueMap;
                /// <summary>資料庫欄位值轉Enum的對應</summary>
                private readonly ReadOnlyDictionary<TValue, TEnum> toEnumMap;
                /// <summary>當TValue為null時對應的Enum，null表示沒對應。</summary>
                private readonly TEnum? nullValue;
                public override object NullValue { get { return nullValue; } }

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
                    TEnum val;
                    return toEnumMap.TryGetValue((TValue)value, out val) ? val : default(TEnum);
                }

                public override void SetValue(IDbDataParameter parameter, object vEnum)
                {
                    TValue value;
                    bool isNull;
                    if (!TryGetValue((TEnum)vEnum, out value, out isNull)) throw new Exception($"未定義{vEnum}的對應值");
                    parameter.Value = isNull ? (object)DBNull.Value : value;
                }
            }

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

                #region value to enum
                public static TEnum ValueToEnum(object value)
                {
                    return (TEnum)Handler.Parse(typeof(TEnum), value);
                }
                #endregion
            }

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

            internal static MethodInfo GetEnumGetterMethod(Type memberType, out object nullValue)
            {
                bool isNullableEnum;
                var handler = GetHandler(memberType, out isNullableEnum);
                nullValue = handler?.NullValue;
                return handler?.valueToEnum;
            }

            /// <summary>取得 Enum 或 Enum? 轉成 Value 的MethodInfo。</summary>
            /// <param name="memberType"></param>
            /// <param name="valueType"></param>
            /// <returns>如果未設定ValueAttribute則回傳null。</returns>
            internal static MethodInfo GetValueGetterMethod(Type memberType, out Type valueType)
            {
                valueType = null;
                bool isNullableEnum;
                var handler = GetHandler(memberType, out isNullableEnum);
                if (handler == null) return null;
                valueType = handler.ValueType;
                return isNullableEnum ? handler.nullEnumToValue : handler.enumToValue;
            }


            /// <summary>取得 IEnumerable&lt;Enum&gt; 或 IEnumerable&lt;Enum?&gt; 轉成 IEnumerable&lt;Value&gt; 的MethodInfo。</summary>
            /// <param name="memberType"></param>
            /// <param name="valueType"></param>
            /// <returns>如果未設定ValueAttribute則回傳null。</returns>
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

            /// <summary>取得Enum對應Value的基礎型別。</summary>
            /// <param name="enumType"></param>
            /// <returns>如果未設定ValueAttribute則回傳null。</returns>
            internal static Type GetValueUnderlyingType(Type enumType)
            {
                bool isNullableEnum;
                var handler = GetHandler(enumType, out isNullableEnum);
                return handler?.ValueUnderlyingType;
            }

        }
    }
}
