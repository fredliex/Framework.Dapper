using Framework.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;
using System.Data;

namespace Framework.Test
{
    public sealed class DbCommandIntercept : IDbCommandIntercept
    {
        public T CommandExecute<T>(DbCommand command, Func<DbCommand, T> executeDelegate)
        {
            var trace = DbTraceContext.Current;
            //如果沒有DbTraceContext或是Add回傳true的話表示要執行真正的sql
            var executeRealCommand = DbTraceContext.Current?.Add(command) ?? true;
            //執行真正的sql
            if (executeRealCommand) return executeDelegate(command);
            //不執行真正sql, 傳回假資料
            if (typeof(IDataReader).IsAssignableFrom(typeof(T)))
            {
                //隨便下一個不需要table的sql
                var conn = command.Connection;
                if (!(conn is System.Data.SqlClient.SqlConnection)) throw new NotImplementedException($"目前未處理{conn.GetType().Name}的假Command測試");
                var newCommand = conn.CreateCommand();
                newCommand.CommandType = CommandType.Text;
                newCommand.CommandText = "select 1 as a";
                return (T)(object)newCommand.ExecuteReader();
            }
            return default(T);
        }
    }
}
