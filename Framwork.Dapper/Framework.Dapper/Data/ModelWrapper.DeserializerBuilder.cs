//#define saveParamAssembly

using Dapper;
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
        internal static class DeserializerBuilder
        {
            //仿dapper, 空物件, 用來定義沒對應的型別
            internal sealed class DontMap { }

            private static readonly MethodInfo getItem = typeof(IDataRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => p.GetIndexParameters().Any() && p.GetIndexParameters()[0].ParameterType == typeof(int))
                        .Select(p => p.GetGetMethod()).First();
            private static readonly MethodInfo trimRight = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.TrimRight), BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo readChar = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.ReadChar), BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo readGuid = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.ReadGuid), BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly MethodInfo parseEnum = typeof(DeserializerBuilder).GetMethod(nameof(DeserializerBuilder.ParseEnum), BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);

            private static string TrimRight(string str)
            {
                return str.Trim(' ');
            }

            private static char ReadChar(object value)
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

            private static object ParseEnum(Type type, string value, bool allowNull)
            {
                if (value == null)
                {
                    if (allowNull) return null;
                    throw new Exception($"無法將null轉成型別{type}");
                }
                return Enum.Parse(type, value, false);
            }

            // 仿dapper裡面的GenerateDeserializers
            internal static Func<IDataReader, object>[] GetDeserializers(Type[] types, string splitOn, IDataReader reader)
            {
                var deserializers = new List<Func<IDataReader, object>>();
                var splits = splitOn.Split(',').Select(s => s.Trim()).ToArray();
                bool isMultiSplit = splits.Length > 1;
                if (types.First() == typeof(object))
                {
                    // we go left to right for dynamic multi-mapping so that the madness of TestMultiMappingVariations
                    // is supported
                    bool first = true;  //是否為第一個type
                    int currentPos = 0; //目前欄位索引
                    int splitIdx = 0;   //type切割欄位索引
                    string currentSplit = splits[splitIdx]; //type切割欄位名稱
                    foreach (var type in types)
                    {
                        if (type == typeof(DontMap)) break;

                        int splitPoint = Reflect.Dapper.GetNextSplitDynamic(currentPos, currentSplit, reader);     //取得下一個切割欄位索引
                        if (isMultiSplit && splitIdx < splits.Length - 1) currentSplit = splits[++splitIdx];
                        deserializers.Add((GetDeserializer(type, reader, currentPos, splitPoint - currentPos, !first)));
                        currentPos = splitPoint;
                        first = false;
                    }
                }
                else
                {
                    // in this we go right to left through the data reader in order to cope with properties that are
                    // named the same as a subsequent primary key that we split on
                    int currentPos = reader.FieldCount;
                    int splitIdx = splits.Length - 1;
                    var currentSplit = splits[splitIdx];
                    for (var typeIdx = types.Length - 1; typeIdx >= 0; --typeIdx)
                    {
                        var type = types[typeIdx];
                        if (type == typeof(DontMap)) continue; 

                        int splitPoint = 0;
                        if (typeIdx > 0)
                        {
                            splitPoint = Reflect.Dapper.GetNextSplit(currentPos, currentSplit, reader);
                            if (isMultiSplit && splitIdx > 0) currentSplit = splits[--splitIdx];
                        }

                        deserializers.Add((GetDeserializer(type, reader, splitPoint, currentPos - splitPoint, typeIdx > 0)));
                        currentPos = splitPoint;
                    }
                    deserializers.Reverse();
                }
                return deserializers.ToArray();
            }

            internal static Func<IDataReader, object> GetDeserializer(Type type, IDataReader reader, int startBound = 0, int length = -1, bool returnNullIfFirstMissing = false)
            {
                if (type == typeof(object)) return Reflect.Dapper.GetDapperRowDeserializer(reader, startBound, length, returnNullIfFirstMissing);
                var nullableType = type.IsValueType ? Nullable.GetUnderlyingType(type) : null;
                var effectiveType = nullableType ?? type;
                if (Reflect.Dapper.typeMap.ContainsKey(type) || type.FullName == Reflect.Dapper.LinqBinary) 
                    return Reflect.Dapper.GetStructDeserializer(type, effectiveType, startBound);
                if (effectiveType.IsEnum)
                {
                    object enumNullValue;
                    var fromValueToEnum = EnumValueHelper.GetEnumGetterMethod(effectiveType, out enumNullValue);
                    if (fromValueToEnum != null)
                    {
                        // r => fromValueToEnum(r.GetValue(startBound))
                        var expParmeter = Expression.Parameter(typeof(IDataReader));
                        var expBody = Expression.Call(fromValueToEnum, Expression.Call(expParmeter, typeof(IDataReader).GetMethod(nameof(IDataReader.GetValue)), new[] { Expression.Constant(startBound) }));
                        return Expression.Lambda<Func<IDataReader, object>>(expBody, new[] { expParmeter }).Compile();
                    }
                    if (reader.GetFieldType(startBound) == typeof(string))
                    {
                        var allowNull = nullableType != null;
                        return r => ParseEnum(effectiveType, r.IsDBNull(0) ? null : r.GetString(startBound), allowNull);
                    }
                    return Reflect.Dapper.GetStructDeserializer(type, effectiveType, startBound);
                }
                return GetTypeDeserializer(type, reader, startBound, length, returnNullIfFirstMissing);
            }

            private static Func<IDataReader, object> GetTypeDeserializer(Type type, IDataReader reader, int startBound = 0, int length = -1, bool returnNullIfFirstMissing = false)
            {
                var returnType = type.IsValueType ? typeof(object) : type;

#if saveParamAssembly
                var assemblyName = new AssemblyName(string.Format("WrapParamInfo", type.Name.StartsWith("<") ? Guid.NewGuid().ToString("N") : type.Name));
                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");
                var builder = moduleBuilder.DefineType(type + "TypeDeserializer", TypeAttributes.Public);
                var dm = builder.DefineMethod("DynamicCreate", MethodAttributes.Public | MethodAttributes.Static, returnType, new[] { typeof(IDataReader) });
#else
                var dm = new DynamicMethod("Deserialize" + Guid.NewGuid().ToString(), returnType, new[] { typeof(IDataReader) }, type, true);
#endif

                var il = dm.GetILGenerator();

                il.DeclareLocal(typeof(int));       //區域變數0, 目前處理欄位索引
                il.DeclareLocal(type);              //區域變數1, 回傳的型別
                //設定區域變數0 = 0
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
                int index = startBound;
                ConstructorInfo specializedConstructor = null;
                bool supportInitialize = false;

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
                        string proposedTypes = "(" + string.Join(", ", types.Select((n, i) => n.FullName + " " + names[i]).ToArray()) + ")";
                        throw new InvalidOperationException($"A parameterless default constructor or one matching signature {proposedTypes} is required for {type.FullName} materialization");
                    }

                    if (ctor.GetParameters().Length == 0)
                    {
                        //設定區域變數1 = new Model()
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

                var table = TableInfo.Get(type);
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
                 *                                                                                      17.     將value從stack移除
                 * if (memberType has EnumValue.NullValue) {                                            18.     value = EnumValue.NullValue
                 *    if (memberType is Nullable<>)                                                     19.     value = new Nullable<T>(value)
                 *    if (非建構式)                                                                     20.     prop = value
                 * } else {                                                                             21.     將target從stack移除
                 *    if (是建構式) {                                                                   22.     value = null
                 * }
                 *                                                                                      23. finishLabel:
                 */

                var allDone = il.DefineLabel();
                int valueCopyLocal = il.DeclareLocal(typeof(object)).LocalIndex;     //valueCopyLocal是區域變數2, 放value值
                foreach (var item in table.Columns)
                {
                    if (item != null)
                    {
                        if (specializedConstructor == null) il.Emit(OpCodes.Dup); // stack is now [target][target]
                        Label isDbNullLabel = il.DefineLabel();
                        Label finishLabel = il.DefineLabel();
                        il.Emit(OpCodes.Ldarg_0);                       // stack is now [target][target][reader]
                        Reflect.Dapper.EmitInt32(il, index);            // stack is now [target][target][reader][index]
                        il.Emit(OpCodes.Dup);                           // stack is now [target][target][reader][index][index]
                        il.Emit(OpCodes.Stloc_0);                       // stack is now [target][target][reader][index]
                        il.Emit(OpCodes.Callvirt, getItem);             // stack is now [target][target][value-as-object]
                        il.Emit(OpCodes.Dup);                           // stack is now [target][target][value-as-object][value-as-object]
                        Reflect.Dapper.StoreLocal(il, valueCopyLocal);  // stack is now [target][target][value]
                        Type colType = reader.GetFieldType(index);
                        Type memberType = item.ElementType;
                        var nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
                        object enumNullValue = null;

                        //01. if (value == DBNull.Value) goto isDbNullLabel:
                        il.Emit(OpCodes.Dup);                           // stack is now [target][target][value][value]
                        il.Emit(OpCodes.Isinst, typeof(DBNull));        // stack is now [target][target][value][DBNull or null]
                        il.Emit(OpCodes.Brtrue_S, isDbNullLabel);       // stack is now [target][target][value]

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
                            //如果欄位非文字, 且NullMapping是Enum的話, 用數字判斷
                            var nullMappingEnum = colType == typeof(string) ? null : item.NullMapping as Enum;
                            var nullMappingText = nullMappingEnum?.ToString("D") ?? item.NullMapping.ToString();
                            il.EmitConstant(nullMappingText);   // stack is now [target][target][value][value-ToString][NullMapping-text]
                            il.EmitCall(OpCodes.Call, Reflect.String_Equals, null); // stack is now [target][target][value][bool]
                            il.Emit(OpCodes.Brtrue_S, isDbNullLabel); // stack is now [target][target][value]
                        }

                        var isNullableConstructor = nullUnderlyingType != null; //nullable是否透過建構式來建立
                        if (memberType == typeof(char) || memberType == typeof(char?))
                        {
                            //04. 如果member是char或是char? ， value = ReadChar(value)
                            il.EmitCall(OpCodes.Call, readChar, null); // stack is now [target][target][typed-value]
                        }
                        else if (memberType == typeof(Guid) || memberType == typeof(Guid?))
                        {
                            //04. 如果member是Guid或是Guid? ， value = ReadGuid(value)
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
                                    isNullableConstructor = false;      //nullable的話已直接轉型, 所以不須透建構式
                                }
                                else
                                {
                                    //12. 型別不一致，必須透過中介轉型
                                    Reflect.Dapper.FlexibleConvertBoxedFromHeadOfStack(il, colType, nullUnderlyingType ?? memberType, null);
                                }
                            }
                        }
                        //13. 如果是Nullable<> ， value = new Nullable<T>(value)
                        if (isNullableConstructor) il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]

                        //14. 非建構式的話， prop = value
                        if (specializedConstructor == null) item.GenerateSetEmit(il); //stack is now[target]

                        //15. goto finishLabel:
                        il.Emit(OpCodes.Br_S, finishLabel); // stack is now [target]

                        //16. 標記 isDbNullLabel:
                        il.MarkLabel(isDbNullLabel); // incoming stack: [target][target][value]

                        //17. 移除value
                        il.Emit(OpCodes.Pop);   // stack is now [target][target]

                        if (enumNullValue != null)
                        {
                            //18. 如果有定義EnumValue.NullValue ， value = EnumValue.NullValue
                            il.EmitConstant(enumNullValue);   // stack is now [target][target][value]

                            //19. 如果是Nullable<> ， value = new Nullable<T>(value)
                            if (nullUnderlyingType != null) il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // stack is now [target][target][typed-value]

                            //20. 非建構式的話， prop = value
                            if (specializedConstructor == null) item.GenerateSetEmit(il);   //stack is now[target]
                        }
                        else
                        {
                            //21. 將target從stack移除
                            il.Emit(OpCodes.Pop);   // stack is now [target]
                            if (specializedConstructor != null)
                            {
                                //22. value = null
                                il.Emit(OpCodes.Ldnull); // stack is now [target][null]
                            }
                        }
                        il.MarkLabel(finishLabel);
                    }
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

#if saveParamAssembly
                var t = builder.CreateType();
                assemblyBuilder.Save(assemblyName.Name + ".dll");
                return (Func<IDataReader, object>)Delegate.CreateDelegate(typeof(Func<IDataReader, object>), t.GetMethod(dm.Name));
#else
                var funcType = System.Linq.Expressions.Expression.GetFuncType(typeof(IDataReader), returnType);
                return (Func<IDataReader, object>)dm.CreateDelegate(funcType);
#endif
            }
        }
    }
}
