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
            sealed class TypeMapp : SqlMapper.ITypeMap
            {
                public ConstructorInfo FindConstructor(string[] names, Type[] types)
                {
                    throw new NotImplementedException();
                }

                public ConstructorInfo FindExplicitConstructor()
                {
                    return null;
                }

                public IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
                {
                    throw new NotImplementedException();
                }

                public IMemberMap GetMember(string columnName)
                {
                    throw new NotImplementedException();
                }
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
                    for(var i = 0; i < length; i++) types[i] = reader.GetFieldType(startBound + i);
                    //尋找名稱與型別相符的建構式
                    var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .OrderBy(c => c.IsPublic ? 0 : (c.IsPrivate ? 2 : 1)).ThenBy(c => c.GetParameters().Length)
                        .FirstOrDefault(c => {
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
                            il.EmitCall(OpCodes.Callvirt, typeof(ISupportInitialize).GetMethod(nameof(ISupportInitialize.BeginInit)), null);
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



                    /* 
                     *                                                                                      01.     if (value == DBNull.Value) goto isDbNullLabel:
                     * if (colType == string && member has ColumnAttribute.IsTrimRight)                             value = value.TrimEnd();
                     * if (member has ColumnAttribute.NullMapping)                                                  if (value == ColumnAttribute.NullMapping) goto isDbNullLabel:
                     * if (memberType is Guid?) {                                                                   value = SqlMapper.ReadGuid/ReadNullableGuid(value);
                     * } else if (memberType is char?) {                                                            value = SqlMapper.ReadChar/ReadNullableChar(value);
                     * } else if (memberType is System.Data.Linq.Binary) {                                          value = new System.Data.Linq.Binary(value);
                     * } else if (memberType is Enum?) {
                     *    if (memberType has ValueAttrubite) {                                                      value = fromValueToEnum(value);
                     *    } else {                                                                                  value = (memberType)(Enum基礎型別)value;
                     *    }
                     * }
                     * 
                     * 
                     * 
                     * 
                     * if (memberType is System.Data.Linq.Binary)                                                   value = new System.Data.Linq.Binary(value);
                     *    if (colType == string) {                                                                  value = Guid.Parse(value)
                     *    } else {                                                                                  value = new Guid((byte[])value)
                     *    }
                     * }
                     *    
                     *    
                     *    if (memberType is Enum) {
                     *       if (memberType has ValueAttrubite) {                                                   value = fromValueToEnum(value);
                     *       else {                                                                                 value = (memberType)(Enum基礎型別)value;
                     *       }
                     *    }
                     *    if (memberType has ValueAttrubite) {                                                      value = fromValueToEnum(value);
                     *    } else if (memberType != string && memberType has Parse method)                           value = memberType.Parse(value);
                     *    }
                     * } else {
                     *    if ()
                     * }
                     * if (memberType is System.Data.Linq.Binary)                                                   value = new System.Data.Linq.Binary(value);
                     * 
                     * 
                     *                                                                                      09. isDbNullLabel:
                     *                                                                                      02.     value = null; 
                     *                                                                                              
                     *                                                                                      value = null;
                     *                                                                                      
                     *                                                                                      
                     * 
                     * 
                     * 
                     * 
                     * if (col is DBNull) {
                     *    if (member's type has ValueAttrubite && has EnumValue.NullValue) {                01.    value = EnumValue.NullValue;
                     *    } else                                                                            02.    value = default(memberType);
                     *    }
                     * } else {
                     *    if (col is string && member has IsTrimRight)                                      03.    value.TrimEnd();
                     *    if (col has define ColumnAtrribute.NullMapping) {                                 04.    if (value == ColumnAtrribute.NullMapping) value = null;
                     *    } else {
                     *    
                     *    }
                     * }
                     * 
                     * 
                     * 
                     */

                    /* 
                     * if(value is DBNull) {
                     *      替換stack。若memberType是有對應的enum, 則替換成DBNull所對應的enum；否則替換成default(memberType)。
                     * } else {
                     *      if(IsTrimRight && dbType == string) value = value.TrimEnd();
                     *      if(dbValue == NullValue) {
                     *          將stack替換成default(memberType)
                     *      } else {
                     *          var targetType = memberType;
                     *          if(member的型態是有對應的Enum) targetType = enumValueType;
                     *          將stack上的value轉型到targetType
                     *          if(member的型態是有對應的Enum) 將stack替換成對應的Enum;
                     *      }
                     * }
                     * if(屬性建立的話) 設屬性值
                     * 
                     * 依照上述規則巡迴所有member之後...
                     * if(建構建立的話) ctor(...)
                     */



                }
            }





        }
    }
}
