using Dapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class ModelWrapper
    {
        internal sealed class Cache
        {
            #region static 
            private static ConcurrentDictionary<SqlMapper.Identity, Cache> storage = new ConcurrentDictionary<SqlMapper.Identity, Cache>();
            public static Cache GetCache(IDbConnection conn, CommandType commandType, string commandText, Type paramType, Func<Func<object, object>> paramWrapperGetter)
            {
                var identity = ModelWrapper.Reflect.Dapper.NewIdentity(commandText, commandType, conn, paramType, paramType, null);
                return storage.GetOrAdd(identity, x => new Cache { ParamWrapper = paramWrapperGetter() });
            }
            #endregion

            public Func<object, object> ParamWrapper { get; private set; }
            private readonly ConcurrentDictionary<int, Func<IDataReader, object>[]> deserializers = new ConcurrentDictionary<int, Func<IDataReader, object>[]>();
            public Func<IDataReader, object>[] GetOrAddDeserializer(Type[] resultTypes, Func<Type[], Func<IDataReader, object>[]> valueFactory)
            {
                var hashCode = 17;
                if (resultTypes != null)
                {
                    foreach (var t in resultTypes)
                    {
                        hashCode = hashCode * 23 + (t?.GetHashCode() ?? 0);
                    }
                }
                return deserializers.GetOrAdd(hashCode, x => valueFactory(resultTypes));
            }
        }
    }
}
