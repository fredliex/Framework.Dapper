using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
            internal static void Register(Type modelType)
            {
                DapperInjector.GetCache(modelType);
            }

        }
    }
}
