﻿using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace Framework.Data
{
    partial class ModelWrapper
    {
        //http://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method#comment64907608_36415711

        internal static class DapperInjector
        {
            private static void ReplaceDapperMethod(MethodInfo methodToReplace, MethodInfo methodToInject)
            {
                RuntimeHelpers.PrepareMethod(methodToReplace.MethodHandle);
                RuntimeHelpers.PrepareMethod(methodToInject.MethodHandle);
                unsafe
                {
                    if (IntPtr.Size == 4)
                    {
                        int* inj = (int*)methodToInject.MethodHandle.Value.ToPointer() + 2;
                        int* tar = (int*)methodToReplace.MethodHandle.Value.ToPointer() + 2;
#if DEBUG
                        byte* injInst = (byte*)*inj;
                        byte* tarInst = (byte*)*tar;
                        int* injSrc = (int*)(injInst + 1);
                        int* tarSrc = (int*)(tarInst + 1);
                        *tarSrc = (((int)injInst + 5) + *injSrc) - ((int)tarInst + 5);
#else
                        *tar = *inj;
#endif
                    }
                    else
                    {
                        ulong* inj = (ulong*)methodToInject.MethodHandle.Value.ToPointer() + 1;
                        ulong* tar = (ulong*)methodToReplace.MethodHandle.Value.ToPointer() + 1;
#if DEBUG
                        byte* injInst = (byte*)*inj;
                        byte* tarInst = (byte*)*tar;
                        int* injSrc = (int*)(injInst + 1);
                        int* tarSrc = (int*)(tarInst + 1);
                        *tarSrc = (((int)injInst + 5) + *injSrc) - ((int)tarInst + 5);
#else
                        *tar = *inj;
#endif
                    }
                }
            }

            internal sealed class DeserializerReaders<T> : Dictionary<T, Func<IDataReader, object>>, IDictionary<T, Func<IDataReader, object>>
            {
                public new bool TryGetValue(T key, out Func<IDataReader, object> value)
                {
                    value = null;
                    return true;
                }
            }

            private readonly static Hashtable deserializerCache;
            private readonly static Func<Type, object> createCache;

            private static Func<IDataReader, object> GetTypeDeserializerImpl(Type type, IDataReader reader, int startBound = 0, int length = -1, bool returnNullIfFirstMissing = false)
            {
                throw new Exception("AAAAAAAAAAAA");
                return null;
            }

            static DapperInjector()
            {
                var methodGetTypeDeserializerImpl = typeof(SqlMapper).GetMethod("GetTypeDeserializerImpl", BindingFlags.Static | BindingFlags.NonPublic);
                ReplaceDapperMethod(
                    methodGetTypeDeserializerImpl,
                    typeof(DapperInjector).GetMethod(nameof(GetTypeDeserializerImpl), BindingFlags.Static | BindingFlags.NonPublic)
                );


                return;
                var typeDeserializerCache = typeof(SqlMapper).GetNestedType("TypeDeserializerCache", BindingFlags.NonPublic);   //抓取型別 SqlMapper.TypeDeserializerCache
                InternalHelper.WrapField(typeDeserializerCache, "byType", out deserializerCache);                               //抓取欄位 private static Hashtable, 存放著<Type, TypeDeserializerCache>

                var typeDeserializerKey = typeDeserializerCache.GetNestedType("DeserializerKey", BindingFlags.NonPublic);       //抓取型別 SqlMapper.TypeDeserializerCache.DeserializerKey
                var typeDeserializerReaders = typeof(DeserializerReaders<>).MakeGenericType(typeDeserializerKey);               //

                var cacheConstructor = typeDeserializerCache.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null);
                var expParamContext = Expression.Parameter(typeof(Type), "type");
                var expVarCache = Expression.Variable(typeDeserializerCache, "cache");
                var expBody = new List<Expression>(new Expression[]
                {
                    // var cache = new TypeDeserializerCache(type);
                    Expression.Assign(expVarCache, Expression.New(cacheConstructor, expParamContext)),
                    // cache.readers = new DeserializerReaders<DeserializerKey>();
                    Expression.Assign(
                        Expression.Field(expVarCache, typeDeserializerCache.GetField("readers", BindingFlags.Instance | BindingFlags.NonPublic)),
                        Expression.New(typeDeserializerReaders)
                    ),
                    // return cache
                    expVarCache
                });
                var expBlock = Expression.Block(new[] { expVarCache }, expBody);
                createCache = Expression.Lambda<Func<Type, object>>(expBlock, new[] { expParamContext }).Compile();
            }

            internal static object GetCache(Type type)
            {
                return null;

                //仿SqlMapper.TypeDeserializerCache.GetReader
                var found = deserializerCache[type];
                if (found == null)
                {
                    lock (deserializerCache)
                    {
                        found = deserializerCache[type];
                        if (found == null) deserializerCache[type] = found = createCache(type);
                    }
                }
                return found;
            }
        }

        internal sealed class DeserializerBuilder
        {
            private static readonly MethodInfo getItem = typeof(IDataRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => p.GetIndexParameters().Any() && p.GetIndexParameters()[0].ParameterType == typeof(int))
                        .Select(p => p.GetGetMethod()).First();
            private static readonly MethodInfo trimRight = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.TrimRight), BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo readChar = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.ReadChar), BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo readGuid = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.ReadGuid), BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo parseEnum = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.ParseEnum), BindingFlags.Static | BindingFlags.NonPublic);

            private static string TrimRight(string str)
            {
                return str.Trim(' ');
            }

            private char ReadChar(object value)
            {
                string s = value as string;
                if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", nameof(value));
                return s[0];
            }
            private static Guid ReadGuid(object value)
            {
                if (value is byte[]) return new Guid((byte[])value);
                if (value is string) return Guid.Parse((string)value);
                return (Guid)value;
            }

            private static T ParseEnum<T>(string value)
            {
                return (T)Enum.Parse(typeof(T), value, true);
            }

            public Func<IDataReader, object> GetTypeDeserializer(Type type, IDataReader reader, int startBound = 0, int length = -1, bool returnNullIfFirstMissing = false)
            {
                return null;

                var returnType = type.IsValueType ? typeof(object) : type;
                var dm = new DynamicMethod("Deserialize" + Guid.NewGuid().ToString(), returnType, new[] { typeof(IDataReader) }, type, true);

                var il = dm.GetILGenerator();
                il.DeclareLocal(typeof(int));
                il.DeclareLocal(type);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Stloc_0);

                if (length == -1) length = reader.FieldCount - startBound;
                if (reader.FieldCount <= startBound)
                {
                    bool hasFields = false;
                    try { hasFields = reader?.FieldCount != 0; } catch { }
                    throw hasFields ? 
                        new ArgumentException("When using the multi-mapping APIs ensure you set the splitOn param if you have keys other than Id", "splitOn") :
                        (Exception)new InvalidOperationException("No columns were selected");
                }

                var names = Enumerable.Range(startBound, length).Select(i => reader.GetName(i)).ToArray();

                //ITypeMap typeMap = GetTypeMap(type);

                int index = startBound;

                ConstructorInfo specializedConstructor = null;

                bool supportInitialize = false;
                Dictionary<Type, LocalBuilder> structLocals = null; //存放已定義的區域變數

                if (type.IsValueType)
                {
                    il.Emit(OpCodes.Ldloca_S, (byte)1);
                    il.Emit(OpCodes.Initobj, type);
                }
                else
                {
                    var types = new Type[length];
                    for (var i = 0; i < length; i++) types[i] = reader.GetFieldType(startBound + i);
                    //尋找名稱與型別相符的建構式
                    var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .OrderBy(c => c.IsPublic ? 0 : (c.IsPrivate ? 2 : 1)).ThenBy(c => c.GetParameters().Length)
                        .FirstOrDefault(c =>
                        {
                            var ctorParameters = c.GetParameters();
                            if (ctorParameters.Length == 0) return true;
                            if (ctorParameters.Length != types.Length) return false;
                            for (var i = 0; i < ctorParameters.Length; i++)
                            {
                                if (!String.Equals(ctorParameters[i].Name, names[i], StringComparison.OrdinalIgnoreCase)) return false;
                                var fieldType = types[i];
                                var unboxedType = ctorParameters[i].ParameterType;
                                if (fieldType == typeof(byte[]) && unboxedType.FullName == Reflect.Dapper.LinqBinary) return true;
                                unboxedType = Nullable.GetUnderlyingType(unboxedType) ?? unboxedType;
                                if (fieldType == unboxedType) continue;
                                if (Reflect.Dapper.HasTypeHandler(unboxedType)) continue;
                                if (unboxedType == typeof(char) && fieldType == typeof(string)) continue;
                                if (unboxedType.IsEnum)
                                {
                                    if (fieldType == EnumValueHelper.GetValueUnderlyingType(unboxedType)) continue;
                                    if (Enum.GetUnderlyingType(unboxedType) == fieldType) continue;
                                    if (fieldType == typeof(string)) continue;
                                }
                                return false;
                            }
                            return true;
                        });
                    if (ctor == null)
                    {
                        string proposedTypes = "(" + string.Join(", ", types.Select((t, i) => t.FullName + " " + names[i]).ToArray()) + ")";
                        throw new InvalidOperationException($"A parameterless default constructor or one matching signature {proposedTypes} is required for {type.FullName} materialization");
                    }

                    if (ctor.GetParameters().Length == 0)
                    {
                        il.Emit(OpCodes.Newobj, ctor);
                        il.Emit(OpCodes.Stloc_1);

                        //如果有繼承ISupportInitialize的話, 呼叫ISupportInitialize.BeginInit
                        supportInitialize = typeof(ISupportInitialize).IsAssignableFrom(type);
                        if (supportInitialize)
                        {
                            il.Emit(OpCodes.Ldloc_1);
                            il.EmitCall(OpCodes.Callvirt, Reflect.ISupportInitialize_BeginInit, null);
                        }
                    }
                    else
                    {
                        specializedConstructor = ctor;
                    }

                    il.BeginExceptionBlock();
                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Ldloca_S, (byte)1);// [target]
                    }
                    else if (specializedConstructor == null)
                    {
                        il.Emit(OpCodes.Ldloc_1);// [target]
                    }

                    var table = GetTableInfo(type);
                    var members = names.Select(table.GetColumn);

                    /* model.Enum -> EnumValue.NullValue -> ColumnAttribute.NullMapping -> database
                     * database -> Trim -> ColumnAttribute.NullMapping(特定值轉成null) -> EnumValue.NullValue(null轉成特定enum) -> model.Enum
                     * 
                     * 
                     *                                                                                      01.     if (value == DBNull.Value) goto isDbNullLabel:
                     * if (colType == string && member has ColumnAttribute.IsTrimRight)                     02.     value = value.TrimEnd()
                     * if (member has NullMapping)                                                          03.     if (value == NullMapping) goto isDbNullLabel:
                     * if (memberType is char or char?) {                                                   04.     value = SqlMapper.ReadChar(value)
                     * } else if (memberType is Guid or Guid?) {                                            05.     value = SqlMapper.ReadGuid(value)
                     * } else if (memberType is Enum or Enum?) {
                     *    if (memberType has EnumValue) {                                                   06.     value = getEnumFromValue(value)
                     *    } else if (colType == string) {                                                   07.     value = Enum.Parse(enumType, value, true)
                     *    } else {                                                                          08.     value = (enumType)(Enum基礎型別)value
                     *    }
                     * } else if (memberType is System.Data.Linq.Binary) {                                  09.     value = new System.Data.Linq.Binary(value)
                     * } else if (memberType 有handler) {                                                   10.     value = SqlMapper.TypeHandlerCache<T>.Parse(value)
                     * } else if (型別一致) {                                                               11.     value = (memberType)value
                     * } else {                                                                             12.     呼叫SqlMapper.FlexibleConvertBoxedFromHeadOfStack來轉型, 創建
                     * }
                     * if (memberType is Nullable<>)                                                        13.     value = new Nullable<T>(value)
                     * if (非建構式建立的話)                                                                14.     prop = value
                     *                                                                                      15.     goto finishLabel:
                     *                                                                                      16. isDbNullLabel:
                     * if (memberType has EnumValue.NullValue) {                                            17.     value = EnumValue.NullValue
                     *    if (memberType is Nullable<>)                                                     18.     value = new Nullable<T>(value)
                     * } else if (是建構式) {                                                               19.     value = null
                     * }                                                                                    
                     * if (非建構式) {                                                                      20.     prop = value
                     * else {                                                                               21.     將 value 遺留在stack, 以便最後呼叫建構方時傳入參數
                     * }
                     *                                                                                      19. finishLabel:
                     *                                                                                      
                     */

                    bool first = true;
                    var allDone = il.DefineLabel();
                    int enumDeclareLocal = -1, valueCopyLocal = il.DeclareLocal(typeof(object)).LocalIndex;
                    foreach (var item in members)
                    {
                        if (item != null)
                        {
                            if (specializedConstructor == null) il.Emit(OpCodes.Dup); // stack is now [target][target]
                            Label isDbNullLabel = il.DefineLabel();
                            Label finishLabel = il.DefineLabel();
                            il.Emit(OpCodes.Ldarg_0); // stack is now [target][target][reader]
                            Reflect.Dapper.EmitInt32(il, index); // stack is now [target][target][reader][index]
                            il.Emit(OpCodes.Dup);// stack is now [target][target][reader][index][index]
                            il.Emit(OpCodes.Stloc_0);// stack is now [target][target][reader][index]
                            il.Emit(OpCodes.Callvirt, getItem); // stack is now [target][target][value-as-object]
                            il.Emit(OpCodes.Dup); // stack is now [target][target][value-as-object][value-as-object]
                            Reflect.Dapper.StoreLocal(il, valueCopyLocal);  //stack is now [target][target][value]
                            Type colType = reader.GetFieldType(index);
                            Type memberType = item.ValueType;
                            var nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
                            object enumNullValue = null;

                            //01. if (value == DBNull.Value) goto isDbNullLabel:
                            il.Emit(OpCodes.Dup); // stack is now [target][target][value][value]
                            il.Emit(OpCodes.Isinst, typeof(DBNull)); // stack is now [target][target][value-as-object][DBNull or null]
                            il.Emit(OpCodes.Brtrue_S, isDbNullLabel); // stack is now [target][target][value-as-object]


                            //02. 如果欄位是字串且有定義ColumnAttribute.IsTrimRight    value = value.TrimEnd()
                            if (colType == typeof(string) && item.IsTrimRight)
                            {
                                il.Emit(OpCodes.Unbox_Any, colType); // stack is now [target][target][string]
                                il.EmitCall(OpCodes.Call, trimRight, null); // stack is now [target][target][string]
                            }

                            //03. 如果member有定義NullMapping   if (value == NullMapping) goto isDbNullLabel:
                            if (item.NullMapping != null)
                            {
                                il.Emit(OpCodes.Dup); // stack is now [target][target][value][value]
                                il.EmitCall(OpCodes.Callvirt, typeof(object).GetMethod(nameof(object.ToString)), null);     // stack is now [target][target][value][value-text]
                                il.EmitConstant(item.NullMapping.ToString());   // stack is now [target][target][value][value-ToString][NullMapping-text]
                                il.EmitCall(OpCodes.Call, Reflect.String_Equals, null); // stack is now [target][target][value][bool]
                                il.Emit(OpCodes.Brtrue_S, isDbNullLabel); // stack is now [target][target][value]
                            }

                            //04. 如果member是char或是char? ， value = ReadChar(value)
                            if (memberType == typeof(char) || memberType == typeof(char?))
                            {
                                il.EmitCall(OpCodes.Call, readChar, null); // stack is now [target][target][typed-value]
                            }
                            //04. 如果member是Guid或是Guid? ， value = ReadGuid(value)
                            else if (memberType == typeof(Guid) || memberType == typeof(Guid?))
                            {
                                il.EmitCall(OpCodes.Call, readGuid, null); // stack is now [target][target][typed-value]
                            }
                            else
                            {
                                var unboxType = nullUnderlyingType != null && nullUnderlyingType.IsEnum ? nullUnderlyingType : memberType;  //unboxType不是Enum就是 memberType
                                if (unboxType.IsEnum)
                                {
                                    var fromValueToEnum = EnumValueHelper.GetEnumGetterMethod(unboxType, out enumNullValue);
                                    if (fromValueToEnum != null)
                                    {
                                        //05. 如果memberType有設定ValueAttribute的話, 把value轉成enum
                                        il.EmitCall(OpCodes.Call, fromValueToEnum, null);  // stack is now [target][target][enum-value]
                                    }
                                    else if (colType == typeof(string))
                                    {
                                        //07. 如果欄位是字串, Enum.Parse
                                        il.EmitCall(OpCodes.Call, parseEnum.MakeGenericMethod(unboxType), null);  // stack is now [target][target][enum-value]
                                    }
                                    else
                                    {
                                        //08. value = (enumType)(Enum基礎型別)value
                                        Type numericType = Enum.GetUnderlyingType(unboxType);
                                        Reflect.Dapper.FlexibleConvertBoxedFromHeadOfStack(il, colType, unboxType, numericType);
                                    }
                                }
                                else if (memberType.FullName == Reflect.Dapper.LinqBinary)
                                {
                                    //09. 是System.Data.Linq.Binary的話，new System.Data.Linq.Binary((byte[])value)
                                    il.Emit(OpCodes.Unbox_Any, typeof(byte[])); // stack is now [target][target][byte-array]
                                    il.Emit(OpCodes.Newobj, memberType.GetConstructor(new Type[] { typeof(byte[]) }));// stack is now [target][target][binary]
                                }
                                else if (Reflect.Dapper.HasTypeHandler(unboxType))
                                {
#pragma warning disable 618
                                    //10. 有TypeHandler, 呼叫SqlMapper.TypeHandlerCache<T>.Parse(value)
                                    il.EmitCall(OpCodes.Call, typeof(TypeHandlerCache<>).MakeGenericType(unboxType).GetMethod(nameof(TypeHandlerCache<int>.Parse)), null); // stack is now [parameters] [parameters] [parameter]
#pragma warning restore 618
                                }
                                else 
                                {
                                    TypeCode dataTypeCode = Type.GetTypeCode(colType), unboxTypeCode = Type.GetTypeCode(unboxType);
                                    if (colType == unboxType || dataTypeCode == unboxTypeCode || dataTypeCode == Type.GetTypeCode(nullUnderlyingType))
                                    {
                                        //11. 型別一致， value = (memberType)value
                                        il.Emit(OpCodes.Unbox_Any, unboxType); // stack is now [target][target][typed-value]
                                    }
                                    else
                                    {
                                        //12. 轉型
                                        Reflect.Dapper.FlexibleConvertBoxedFromHeadOfStack(il, colType, nullUnderlyingType ?? memberType, null);
                                    }
                                }
                            }
                            //13. 如果是Nullable<> ， value = new Nullable<T>(value)
                            if (nullUnderlyingType != null) il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]

                            //14. 非建構式的話， prop = value
                            if (specializedConstructor == null) item.EmitGenerateSet(il);

                            //15. goto finishLabel:
                            il.Emit(OpCodes.Br_S, finishLabel); // stack is now [target][target][value]

                            //16. 標記 isDbNullLabel:
                            il.MarkLabel(isDbNullLabel); // incoming stack: [target][target][value]

                            //17. 如果有定義EnumValue.NullValue ， value = EnumValue.NullValue
                            if (specializedConstructor != null || enumNullValue != null)
                            {
                                il.Emit(OpCodes.Pop);   // stack is now [target][target]
                                il.EmitConstant(enumNullValue);   // stack is now [target][target][value]

                                //18. 如果是Nullable<> ， value = new Nullable<T>(value)
                                if (nullUnderlyingType != null) il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]

                                //19. 非建構式的話， prop = value
                                if (specializedConstructor == null) item.EmitGenerateSet(il);
                            }
                        }
                        first = false;
                        index += 1;
                    }


                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Pop);
                    }
                    else
                    {
                        if (specializedConstructor != null)
                        {
                            il.Emit(OpCodes.Newobj, specializedConstructor);
                        }
                        il.Emit(OpCodes.Stloc_1); // stack is empty

                        if (supportInitialize)
                        {
                            il.Emit(OpCodes.Ldloc_1);
                            il.EmitCall(OpCodes.Callvirt, Reflect.ISupportInitialize_EndInit, null);
                        }

                    }
                    il.MarkLabel(allDone);
                    il.BeginCatchBlock(typeof(Exception)); // stack is Exception
                    il.Emit(OpCodes.Ldloc_0); // stack is Exception, index
                    il.Emit(OpCodes.Ldarg_0); // stack is Exception, index, reader
                    Reflect.Dapper.LoadLocal(il, valueCopyLocal); // stack is Exception, index, reader, value
                    il.EmitCall(OpCodes.Call, Reflect.SqlMapper_ThrowDataException, null);

                    il.EndExceptionBlock();

                    il.Emit(OpCodes.Ldloc_1); // stack is [rval]
                    if (type.IsValueType)
                    {
                        il.Emit(OpCodes.Box, type);
                    }
                    il.Emit(OpCodes.Ret);

                    var funcType = System.Linq.Expressions.Expression.GetFuncType(typeof(IDataReader), returnType);
                    return (Func<IDataReader, object>)dm.CreateDelegate(funcType);
                }
            }
        }
    }
}
