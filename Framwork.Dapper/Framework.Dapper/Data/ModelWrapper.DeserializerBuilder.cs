using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class ModelWrapper
    {


        internal static class DapperInjector
        {
            private readonly static Hashtable deserializerCache;
            private readonly static Func<Type, object> createCache;

            static DapperInjector()
            {
                var typeDeserializerCache = typeof(SqlMapper).GetNestedType("TypeDeserializerCache", BindingFlags.NonPublic);   //抓取 SqlMapper.TypeDeserializerCache
                InternalHelper.WrapField(typeDeserializerCache, "byType", out deserializerCache);
                InternalHelper.WrapConstructor(typeDeserializerCache, out createCache);
            }

            internal static object GetCache(Type type)
            {
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
            static DeserializerBuilder()
            {
                InjectDapper();
            }

            private static void InjectDapper()
            {
                //System.Threading.Monitor.Enter(null, )
            }



        }
    }
}
