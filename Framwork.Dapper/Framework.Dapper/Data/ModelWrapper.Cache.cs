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
        internal sealed class Cahce
        {
            private static ConcurrentDictionary<SqlMapper.Identity, Cahce> cache;

            public Action<IDbCommand, object> ParamReader;
            public ConcurrentDictionary<Type, Func<IDataReader, object>> Deserializers;
        }


    }
}
