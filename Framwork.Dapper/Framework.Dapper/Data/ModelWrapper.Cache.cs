﻿using Dapper;
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
            private sealed class DeserializerEqualityComparer : EqualityComparer<Type[]>
            {
                public override bool Equals(Type[] x, Type[] y)
                {
                    throw new NotImplementedException();
                }

                public override int GetHashCode(Type[] obj)
                {
                    throw new NotImplementedException();
                }
            }

            #region static 
            private static ConcurrentDictionary<SqlMapper.Identity, Cache> storage = new ConcurrentDictionary<SqlMapper.Identity, Cache>();
            public static Cache GetCache(IDbConnection conn, CommandType commandType, string commandText, Type paramType, Func<Func<object, object>> paramWrapperGetter)
            {
                var identity = ModelWrapper.Reflect.Dapper.NewIdentity(commandText, commandType, conn, paramType, paramType, null);
                return storage.GetOrAdd(identity, x => new Cache { ParamWrapper = paramWrapperGetter() });
            }
            #endregion



            /// <summary>包裝器，用以將外部傳入的參數包裝成內部丟給Dapper用的參數。</summary>
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
