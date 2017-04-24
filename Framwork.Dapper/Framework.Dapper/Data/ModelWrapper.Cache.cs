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
        /*
         * 以IDbConnection + CommandType + commandText + paramType為一個cache
         * 一個cache會有一個ParamWrapper用來處理傳入物件轉成DbCommand的參數
         * 一個cache會有多個Deserializer，分別為不同回傳類型所對應的deserializer
         */

        internal sealed class Cache
        {
            #region static
            private sealed class SerializerIdentity : IEquatable<SerializerIdentity>
            {
                private readonly string connectionString;
                private readonly CommandType commandType;
                private readonly string sql;
                private readonly Type paramType;
                private readonly int hashCode;  //we *know* we are using this in a dictionary, so pre-compute this

                public SerializerIdentity(IDbConnection conn, CommandType commandType, string sql, Type paramType)
                {
                    connectionString = conn.ConnectionString;
                    this.commandType = commandType;
                    this.sql = sql;
                    this.paramType = paramType;
                    hashCode = InternalHelper.CombineHashCodes(new[] {
                        connectionString == null ? 0 : StringComparer.Ordinal.GetHashCode(connectionString),
                        commandType.GetHashCode(),
                        sql?.GetHashCode() ?? 0,
                        paramType?.GetHashCode() ?? 0
                    });
                }

                public override int GetHashCode() => hashCode;
                public bool Equals(SerializerIdentity other) =>
                    other != null &&
                    connectionString == other.connectionString &&
                    commandType == other.commandType &&
                    sql == other.sql &&
                    paramType == other.paramType;
            }

            private static ConcurrentDictionary<SerializerIdentity, Cache> storage = new ConcurrentDictionary<SerializerIdentity, Cache>();
            public static Cache GetCache(IDbConnection conn, CommandType commandType, string sql, Type paramType, Func<Func<object, object>> paramWrapperGetter) =>
                storage.GetOrAdd(new SerializerIdentity(conn, commandType, sql, paramType), x => new Cache { ParamWrapper = paramWrapperGetter() });
            #endregion

            private sealed class DeserializerComparer : EqualityComparer<Type[]>
            {
                public override bool Equals(Type[] x, Type[] y) => x.SequenceEqual(y);

                public override int GetHashCode(Type[] obj)
                {
                    var hashCodes = new int[obj.Length];
                    for (var i = 0; i < obj.Length; i++)
                    {
                        hashCodes[i] = obj == null ? 0 : obj.GetHashCode();
                    }
                    return InternalHelper.CombineHashCodes(hashCodes);
                }
            }

            /// <summary>包裝器，用以將外部傳入的參數包裝成內部丟給Dapper用的參數。</summary>
            public Func<object, object> ParamWrapper { get; private set; }

            private readonly ConcurrentDictionary<Type[], Func<IDataReader, object>[]> deserializers = 
                new ConcurrentDictionary<Type[], Func<IDataReader, object>[]>(new DeserializerComparer());
            public Func<IDataReader, object>[] GetDeserializer(Type[] resultTypes, Func<Type[], Func<IDataReader, object>[]> valueFactory) =>
                deserializers.GetOrAdd(resultTypes, x => valueFactory(resultTypes));
        }
    }
}
