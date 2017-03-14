using Framework.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace Framework.Test
{
    public sealed class DbCommandIntercept : IDbCommandIntercept
    {
        public T CommandExecute<T>(DbCommand command, Func<DbCommand, T> executeDelegate)
        {
            DbTraceContext.Current?.Add(command);
            return executeDelegate(command);
        }
    }
}
