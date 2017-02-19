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
        /// <summary>列舉對應資訊</summary>
        internal sealed class EnumInfo
        {
            #region static & type define
            private static class ConverterHelper
            {
                //使用bit運算來儲存method, isCollection^1 + isNullableEnum^2 + isStructValue^3
                private static MethodInfo[] mappingMethods = new[] {
                    nameof(EnumToClass), nameof(EnumsToClassValues), nameof(NullEnumToClass), nameof(NullEnumsToClassValues),
                    nameof(EnumToStruct), nameof(EnumsToStructValues), nameof(NullEnumToStruct), nameof(NullEnumsToStructValues)
                }.Select(n => typeof(ConverterHelper).GetMethod(n)).ToArray();

                private static MethodInfo[] defaultMethods = new[] {
                    nameof(EnumToUnderly), nameof(EnumToUnderlyValues), nameof(NullEnumToUnderly), nameof(NullEnumsToUnderlyValues)
                }.Select(n => typeof(ConverterHelper).GetMethod(n)).ToArray();

                public static MethodInfo GetMappingConvertMethod(Type enumType, Type valueUnderlyingType, bool isNullableEnum, bool isCollection)
                {
                    var index =
                        (valueUnderlyingType.IsValueType ? 1 << 2 : 0) +
                        (isNullableEnum ? 1 << 1 : 0) +
                        (isCollection ? 1 : 0);
                    return mappingMethods[index].MakeGenericMethod(enumType, valueUnderlyingType);
                }

                public static MethodInfo GetDefaultConvertMethod(Type enumType, Type valueUnderlyingType, bool isNullableEnum, bool isCollection)
                {
                    var index = (isNullableEnum ? 1 << 1 : 0) + (isCollection ? 1 : 0);
                    return defaultMethods[index].MakeGenericMethod(enumType, valueUnderlyingType);
                }


                #region 單個
                public static T EnumToClass<TEnum, T>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class
                {
                    T value;
                    bool isNull;
                    if (DefaultConverter<TEnum>.MappingConverter<T>.TryGetValue(enumValue, out value, out isNull)) return isNull ? null : value;
                    throw new Exception($"未定義{enumValue}的對應值");
                }
                public static T? EnumToStruct<TEnum, T>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct
                {
                    T value;
                    bool isNull;
                    if (DefaultConverter<TEnum>.MappingConverter<T>.TryGetValue(enumValue, out value, out isNull)) return isNull ? (T?)null : value;
                    throw new Exception($"未定義{enumValue}的對應值");
                }

                public static T NullEnumToClass<TEnum, T>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class =>
                    enumValue.HasValue ? EnumToClass<TEnum, T>((TEnum)enumValue) : null;
                public static T? NullEnumToStruct<TEnum, T>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    enumValue.HasValue ? EnumToStruct<TEnum, T>((TEnum)enumValue) : (T?)null;
                #endregion

                #region 集合
                public static IEnumerable<T> EnumsToClassValues<TEnum, T>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class =>
                    enumValues?.Select(EnumToClass<TEnum, T>);
                public static IEnumerable<T?> EnumsToStructValues<TEnum, T>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    enumValues?.Select(EnumToStruct<TEnum, T>);
                public static IEnumerable<T> NullEnumsToClassValues<TEnum, T>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class =>
                    enumValues?.Select(NullEnumToClass<TEnum, T>);
                public static IEnumerable<T?> NullEnumsToStructValues<TEnum, T>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    enumValues?.Select(NullEnumToStruct<TEnum, T>);
                #endregion

                #region Underly
                public static T EnumToUnderly<TEnum, T>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    (T)(object)enumValue;
                public static T? NullEnumToUnderly<TEnum, T>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    enumValue.HasValue ? NullEnumToUnderly<TEnum, T>((TEnum)enumValue) : null;
                public static IEnumerable<T> EnumToUnderlyValues<TEnum, T>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    enumValues?.Select(EnumToUnderly<TEnum, T>);
                public static IEnumerable<T?> NullEnumsToUnderlyValues<TEnum, T>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                    enumValues?.Select(NullEnumToUnderly<TEnum, T>);
                #endregion
            }

            public interface IConverter
            {
                /// <summary>列舉型態。非nullable。</summary>
                Type EnumType { get; }

                /// <summary>對應DbValue的型態。可能會string或是值類型或是nullable。</summary>
                Type ValueType { get; }

                /// <summary>對應DbValue的基礎型態。非nullable。</summary>
                Type ValueUnderlyingType { get; }

                /// <summary>取得Enum轉DbValue的Method</summary>
                /// <param name="isNullableEnum">元素是否為nullable</param>
                /// <param name="isCollection">是否為集合</param>
                /// <returns></returns>
                MethodInfo GetToValueMethod(bool isNullableEnum, bool isCollection);
            }

            #region Converter
            private class DefaultConverter<TEnum> : IConverter where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                public Type ValueType { get; protected set; }
                public Type ValueUnderlyingType { get; protected set; }
                public Type EnumType { get; protected set; }

                public virtual MethodInfo GetToValueMethod(bool isNullableEnum, bool isCollection)
                {
                    var enumType = typeof(TEnum);
                    var underlyingType = Enum.GetUnderlyingType(enumType);
                    return ConverterHelper.GetDefaultConvertMethod(enumType, underlyingType, isNullableEnum, isCollection);
                }
            }
            #endregion

            #region MappingConverter
            private sealed class MappingConverter<TEnum, TDbValue> : DefaultConverter<TEnum> where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                /// <summary>Enum轉資料庫欄位值的對應</summary>
                private static ReadOnlyDictionary<TEnum, TDbValue> toValueMap;
                /// <summary>資料庫欄位值轉Enum的對應</summary>
                private static ReadOnlyDictionary<TDbValue, TEnum> toEnumMap;
                /// <summary>當DbValue為null時對應的Enum，null表示沒null所對應的TEnum。</summary>
                private static TEnum? nullValue;

                public MappingConverter(List<KeyValuePair<object, object>> mapping, object nullValue)
                {
                    //以下都是針對靜態類別設值
                    var tmpValueMap = new Dictionary<TEnum, TDbValue>(mapping.Count);
                    var tmpEnumMap = new Dictionary<TDbValue, TEnum>(mapping.Count);
                    foreach (var n in mapping)
                    {
                        var enumValue = (TEnum)n.Key;
                        var dbValue = (TDbValue)n.Value;
                        tmpValueMap[enumValue] = dbValue;
                        tmpEnumMap[dbValue] = enumValue;
                    }
                    nullValue = (TEnum?)nullValue;
                    toValueMap = new ReadOnlyDictionary<TEnum, TDbValue>(tmpValueMap);
                    toEnumMap = new ReadOnlyDictionary<TDbValue, TEnum>(tmpEnumMap);
                }

                internal static bool TryGetValue(TEnum vEnum, out TDbValue vValue, out bool isNullValue)
                {
                    isNullValue = false;
                    vValue = default(TDbValue);
                    if (nullValue.HasValue && EqualityComparer<TEnum>.Default.Equals(nullValue.GetValueOrDefault(), vEnum))
                    {
                        isNullValue = true;
                        return true;
                    }
                    return toValueMap.TryGetValue(vEnum, out vValue);
                }

                private readonly MethodInfo[] toDbValueMethods = new MethodInfo[4];
                public override MethodInfo GetToValueMethod(bool isNullableEnum, bool isCollection)
                {
                    var index = (isNullableEnum ? 1 << 1 : 0) + (isCollection ? 1 : 0);
                    var method = toDbValueMethods[index];
                    if (method != null) return method;
                    toDbValueMethods[index] = method = ConverterHelper.GetMappingConvertMethod(typeof(TEnum), ValueUnderlyingType, isNullableEnum, isCollection);
                    return method;
                }
            }
            #endregion

            #endregion

            public IConverter Converter { get; private set; }

            /// <summary>列舉型別。非nullable。</summary>
            public Type EnumType { get; private set; }

            /// <summary>對應的資料庫型別。非nullable。</summary>
            public Type DbValueType { get; private set; }

            /// <summary>是否為DbValueAttribute的對應。</summary>
            public bool IsDbValueMapping { get; private set; }

            private EnumInfo() { }

            private static ConcurrentDictionary<Type, EnumInfo> cache = new ConcurrentDictionary<Type, EnumInfo>();
            public static EnumInfo Get(Type enumType)
            {
                return cache.GetOrAdd(enumType, t =>
                {
                    object nullEnum;
                    Type dbValueUnderlyingType;
                    var mapping = DbValueAttribute.GetMapping(t, out nullEnum, out dbValueUnderlyingType);
                    var converter = mapping == null ?
                        (IConverter)typeof(DefaultConverter<>).MakeGenericType(t).GetConstructor(Type.EmptyTypes).Invoke(new object[0]) :
                        (IConverter)typeof(MappingConverter<,>).MakeGenericType(t, dbValueUnderlyingType).GetConstructor(new[] { typeof(List<KeyValuePair<object, object>>), typeof(object) }).Invoke(new object[] { mapping, nullEnum });
                    return new EnumInfo { EnumType = t, DbValueType = dbValueUnderlyingType, Converter = converter, IsDbValueMapping = mapping != null };
                });
            }
        }
    }
}
