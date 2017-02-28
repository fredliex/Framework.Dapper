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
            private static class Factory
            {
                public static class DbValueConverter
                {
                    #region 單個
                    public static T ToClass<TEnum, T>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class
                    {
                        T value;
                        bool isNull;
                        if (DbValueMetadata<TEnum, T>.TryGetValue(enumValue, out value, out isNull)) return isNull ? null : value;
                        throw new Exception($"未定義{enumValue}的對應值");
                    }
                    public static T? ToStruct<TEnum, T>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct
                    {
                        T value;
                        bool isNull;
                        if (DbValueMetadata<TEnum, T>.TryGetValue(enumValue, out value, out isNull)) return isNull ? (T?)null : value;
                        throw new Exception($"未定義{enumValue}的對應值");
                    }

                    public static T NullToClass<TEnum, T>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class =>
                        enumValue.HasValue ? ToClass<TEnum, T>((TEnum)enumValue) : null;
                    public static T? NullToStruct<TEnum, T>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        enumValue.HasValue ? ToStruct<TEnum, T>((TEnum)enumValue) : (T?)null;
                    #endregion

                    #region 集合
                    public static IEnumerable<T> ToClassValues<TEnum, T>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class =>
                        enumValues?.Select(ToClass<TEnum, T>);
                    public static IEnumerable<T?> ToStructValues<TEnum, T>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        enumValues?.Select(ToStruct<TEnum, T>);
                    public static IEnumerable<T> NullToClassValues<TEnum, T>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : class =>
                        enumValues?.Select(NullToClass<TEnum, T>);
                    public static IEnumerable<T?> NullToStructValues<TEnum, T>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        enumValues?.Select(NullToStruct<TEnum, T>);
                    #endregion

                    //使用bit運算來儲存method, isCollection^0 + isNullableEnum^1 + isStructValue^2
                    private static MethodInfo[] methods = new[] {
                        nameof(ToClass), nameof(ToClassValues), nameof(NullToClass), nameof(NullToClassValues),
                        nameof(ToStruct), nameof(ToStructValues), nameof(NullToStruct), nameof(NullToStructValues)
                    }.Select(n => typeof(DbValueConverter).GetMethod(n)).ToArray();
                    public static MethodInfo Get(Type enumType, Type valueUnderlyingType, bool isNullableEnum, bool isCollection)
                    {
                        var index =
                            (valueUnderlyingType.IsValueType ? 1 << 2 : 0) +
                            (isNullableEnum ? 1 << 1 : 0) +
                            (isCollection ? 1 : 0);
                        return methods[index].MakeGenericMethod(enumType, valueUnderlyingType);
                    }
                }

                public static class UnderlyConverter
                {
                    public static T ToUnderly<TEnum, T>(TEnum enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        (T)(object)enumValue;
                    public static T? NullToUnderly<TEnum, T>(TEnum? enumValue) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        enumValue.HasValue ? ToUnderly<TEnum, T>((TEnum)enumValue) : (T?)null;
                    public static IEnumerable<T> ToUnderlyValues<TEnum, T>(IEnumerable<TEnum> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        enumValues?.Select(ToUnderly<TEnum, T>);
                    public static IEnumerable<T?> NullToUnderlyValues<TEnum, T>(IEnumerable<TEnum?> enumValues) where TEnum : struct, IComparable, IFormattable, IConvertible where T : struct =>
                        enumValues?.Select(NullToUnderly<TEnum, T>);

                    //使用bit運算來儲存method, isCollection^0 + isNullableEnum^1
                    private static MethodInfo[] methods = new[] { nameof(ToUnderly), nameof(ToUnderlyValues), nameof(NullToUnderly), nameof(NullToUnderlyValues) }
                        .Select(n => typeof(UnderlyConverter).GetMethod(n)).ToArray();
                    public static MethodInfo Get(Type enumType, Type valueUnderlyingType, bool isNullableEnum, bool isCollection)
                    {
                        var index = (isNullableEnum ? 1 << 1 : 0) + (isCollection ? 1 : 0);
                        return methods[index].MakeGenericMethod(enumType, valueUnderlyingType);
                    }
                }

                public static class ValueParser
                {
                    public static TEnum WithoutCheck<TEnum>(object value) where TEnum : struct, IComparable, IFormattable, IConvertible => (TEnum)value;
                    public static TEnum? ToNullableWithoutCheck<TEnum>(object value) where TEnum : struct, IComparable, IFormattable, IConvertible => (TEnum)value;
                    public static TEnum WithCheck<TEnum>(object value) where TEnum : struct, IComparable, IFormattable, IConvertible
                    {
                        if (value == null || value == DBNull.Value) throw new Exception($"null不可轉為{typeof(TEnum)}");
                        return (TEnum)value;
                    }
                    public static TEnum? ToNullableWithCheck<TEnum>(object value) where TEnum : struct, IComparable, IFormattable, IConvertible
                    {
                        if (value == null || value == DBNull.Value) return null;
                        return (TEnum)value;
                    }

                    //使用bit運算來儲存method, checkDbnull^0 + returnNullable^1
                    private static MethodInfo[] methods = new[] { nameof(WithoutCheck), nameof(WithCheck), nameof(ToNullableWithoutCheck), nameof(ToNullableWithCheck) }
                        .Select(n => typeof(ValueParser).GetMethod(n)).ToArray();
                    internal static MethodInfo Get(Type enumType, bool checkDbnull, bool returnNullable)
                    {
                        var index = (returnNullable ? 1 << 1 : 0) + (checkDbnull ? 1 : 0);
                        return methods[index].MakeGenericMethod(enumType);
                    }
                }

                public static class StringParser
                {
                    public static TEnum Parse<TEnum>(object value) where TEnum : struct, IComparable, IFormattable, IConvertible
                    {
                        if (value == null || value == DBNull.Value) throw new Exception($"無法將null轉成{typeof(TEnum)}");
                        return (TEnum)Enum.Parse(typeof(TEnum), (string)value, true);
                    }
                    public static TEnum? ParseNullable<TEnum>(object value) where TEnum : struct, IComparable, IFormattable, IConvertible
                    {
                        if (value == null || value == DBNull.Value) return null;
                        return (TEnum)Enum.Parse(typeof(TEnum), (string)value, true);
                    }
                    private static MethodInfo[] methods = new[] { nameof(Parse), nameof(ParseNullable) }
                        .Select(n => typeof(StringParser).GetMethod(n)).ToArray();
                    internal static MethodInfo Get(Type enumType, bool allowNull)
                    {
                        var index = allowNull ? 1 : 0;
                        return methods[index].MakeGenericMethod(enumType);
                    }
                }
            }

            public abstract class MetadataBase
            {
                /// <summary>列舉型態。非nullable。</summary>
                public Type EnumType { get; private set; }

                /// <summary>Enum的基礎型態。或是DbValue的非nullable型態。</summary>
                public Type UnderlyingValueType { get; private set; }

                /// <summary>UnderlyingValueType的nullable型態。如果UnderlyingValueType是物件類型的話，會同UnderlyingValueType。</summary>
                public Type NullableValueType { get; private set; }

                protected MetadataBase(Type enumType, Type underlyingValueType, Type nullableValueType)
                {
                    EnumType = enumType;
                    UnderlyingValueType = underlyingValueType;
                    NullableValueType = nullableValueType;
                }

                #region GetConverter
                private readonly MethodInfo[] converters = new MethodInfo[4];
                protected abstract MethodInfo GetConverterImp(bool isNullableEnum, bool isCollection);
                /// <summary>取得Enum轉Value的Method</summary>
                /// <param name="isNullableEnum">元素是否為nullable</param>
                /// <param name="isCollection">是否為集合</param>
                /// <returns></returns>
                public MethodInfo GetConverter(bool isNullableEnum, bool isCollection)
                {
                    var index = (isNullableEnum ? 1 << 1 : 0) + (isCollection ? 1 : 0);
                    var method = converters[index];
                    if (method != null) return method;
                    converters[index] = method = GetConverterImp(isNullableEnum, isCollection);
                    return method;
                }
                #endregion

                #region GetParser
                private MethodInfo[] parsers = new MethodInfo[4];
                protected abstract MethodInfo GetParserImp(bool checkDbnull, bool returnNullable);
                /// <summary>取得object value 轉成Enum 的Method</summary>
                /// <param name="checkDbnull">是否檢查DbNull的情況，false的話表示不檢查DbNull速度會叫快</param>
                /// <param name="returnNullable">是否回傳Enum?</param>
                /// <returns></returns>
                public MethodInfo GetObjectParser(bool checkDbnull, bool returnNullable)
                {
                    var index = (returnNullable ? 1 << 1 : 0) + (checkDbnull ? 1 : 0);
                    var method = parsers[index];
                    if (method != null) return method;
                    parsers[index] = method = GetParserImp(checkDbnull, returnNullable);
                    return method;
                }
                #endregion

                #region GetStringParser
                private MethodInfo stringParser = null;
                /// <summary>取得string value 轉成Enum 的Method。</summary>
                /// <param name="allowNull">判斷null或是Dbnull就回傳Enum?</param>
                /// <returns></returns>
                public MethodInfo GetStringParser(bool allowNull) => stringParser ?? (stringParser = Factory.StringParser.Get(EnumType, allowNull));
                #endregion
            }

            #region UnderlyMetadata
            private sealed class UnderlyMetadata<TEnum> : MetadataBase where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                private static Type enumType = typeof(TEnum);
                private static Type underlyingValueType = Enum.GetUnderlyingType(enumType);
                private static Type nullableValueType = typeof(Nullable<>).MakeGenericType(underlyingValueType);
                public UnderlyMetadata() : base(enumType, underlyingValueType, nullableValueType) { }

                protected override MethodInfo GetConverterImp(bool isNullableEnum, bool isCollection) => 
                    Factory.UnderlyConverter.Get(enumType, underlyingValueType, isNullableEnum, isCollection);

                protected override MethodInfo GetParserImp(bool checkDbnull, bool returnNullable) => 
                    Factory.ValueParser.Get(enumType, checkDbnull, returnNullable);
            }
            #endregion

            #region DbValueMetadata
            private sealed class DbValueMetadata<TEnum, TDbValue> : MetadataBase where TEnum : struct, IComparable, IFormattable, IConvertible
            {
                /// <summary>Enum轉資料庫欄位值的對應</summary>
                private static ReadOnlyDictionary<TEnum, TDbValue> toValueMap;
                /// <summary>資料庫欄位值轉Enum的對應</summary>
                internal static ReadOnlyDictionary<TDbValue, TEnum> toEnumMap;
                /// <summary>當DbValue為null時對應的Enum，null表示沒null所對應的TEnum。</summary>
                internal static TEnum? nullValue = null;

                private static Type enumType = typeof(TEnum);
                private static Type underlyingValueType = typeof(TDbValue);
                private static Type nullableValueType = underlyingValueType.IsClass ? underlyingValueType : typeof(Nullable<>).MakeGenericType(underlyingValueType);

                public DbValueMetadata(List<KeyValuePair<object, object>> mapping, object nullValue) : base(typeof(TEnum), underlyingValueType, nullableValueType)
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

                protected override MethodInfo GetConverterImp(bool isNullableEnum, bool isCollection) =>
                    Factory.DbValueConverter.Get(enumType, underlyingValueType, isNullableEnum, isCollection);

                #region parse value to Enum
                //public static TEnum? ParseMappingWithDbnull(object value) => nullValue.HasValue && (value == null || value == DBNull.Value) ? nullValue.Value : ParseMapping(value);
                public static TEnum ParseWithoutCheck(object value)
                {
                    TEnum val;
                    if (toEnumMap.TryGetValue((TDbValue)value, out val)) return val;
                    throw new Exception($"{typeof(TEnum)}未定義{value}的DbValueAttribute");
                }
                public static TEnum ParseWithCheck(object value)
                {
                    if(value == null || value == DBNull.Value)
                    {
                        if (!nullValue.HasValue) throw new Exception($"無法將null轉為{typeof(TEnum)}");
                        return nullValue.Value;
                    }
                    return ParseWithoutCheck(value);
                }
                public static TEnum? ParseNullableWithoutCheck(object value)
                {
                    TEnum val;
                    return toEnumMap.TryGetValue((TDbValue)value, out val) ? val : (TEnum?)null;
                }
                public static TEnum? ParseNullableWithCheck(object value)
                {
                    return value == null || value == DBNull.Value ? nullValue : ParseNullableWithoutCheck(value);
                }
                protected override MethodInfo GetParserImp(bool checkDbnull, bool returnNullable)
                {
                    var methodNmae = checkDbnull ?
                        (returnNullable ? nameof(ParseNullableWithCheck) : nameof(ParseWithCheck)) :
                        (returnNullable ? nameof(ParseNullableWithoutCheck) : nameof(ParseWithoutCheck));
                    return GetType().GetMethod(methodNmae);
                }
                #endregion
            }
            #endregion

            #endregion

            public MetadataBase Metadata { get; private set; }

            /// <summary>列舉型別。非nullable。</summary>
            public Type EnumType { get; private set; }

            /// <summary>對應的Enum基礎型別或是DbValue型別。非nullable。</summary>
            public Type ValueType { get; private set; }

            /// <summary>是否為DbValueAttribute的對應。</summary>
            public bool IsDbValueMapping { get; private set; }

            private EnumInfo() { }

            private static ConcurrentDictionary<Type, EnumInfo> cache = new ConcurrentDictionary<Type, EnumInfo>();
            /// <summary>取得Enum資訊。如果非Enum則會回傳null。</summary>
            /// <param name="enumType"></param>
            /// <returns></returns>
            public static EnumInfo Get(Type enumType)
            {
                if (!enumType.IsEnum) return null;
                return cache.GetOrAdd(enumType, t =>
                {
                    object nullEnum;
                    Type dbValueUnderlyingType;
                    var mapping = DbValueAttribute.GetMapping(t, out nullEnum, out dbValueUnderlyingType);
                    if (mapping == null)
                    {
                        var converter = (MetadataBase)typeof(UnderlyMetadata<>).MakeGenericType(t).GetConstructor(Type.EmptyTypes).Invoke(new object[0]);
                        return new EnumInfo { EnumType = t, ValueType = Enum.GetUnderlyingType(enumType), Metadata = converter, IsDbValueMapping = false };
                    }
                    else
                    {
                        var converter = (MetadataBase)typeof(DbValueMetadata<,>).MakeGenericType(t, dbValueUnderlyingType).GetConstructor(new[] { typeof(List<KeyValuePair<object, object>>), typeof(object) }).Invoke(new object[] { mapping, nullEnum });
                        return new EnumInfo { EnumType = t, ValueType = dbValueUnderlyingType, Metadata = converter, IsDbValueMapping = true };
                    }
                });
            }
        }
    }
}
