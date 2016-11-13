using Dapper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>
    /// 
    /// </summary>
    public static class DbHelper
    {
        /// <summary>
        /// 
        /// </summary>
        public static IDbHook DbHook = null;

        internal static T InternalCommandExecuteWrap<T>(IDbCommand command, Func<IDbCommand, T> func)
        {
            var hook = DbHook;
            return hook == null ? func(command) : hook.CommandExecute(command, func);
        }

        public static IDbConnection OpenConnection(string connName)
        {
            var setting = ConfigurationManager.ConnectionStrings[connName];
            var factory = DbProviderFactories.GetFactory(setting.ProviderName);
            var conn = factory.CreateConnection();
            conn.ConnectionString = setting.ConnectionString;
            conn.Open();
            return conn;
        }

        public static IEnumerable<T> Query<T>(this IDbConnection conn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
        {
            ModelWrapper.DeserializerBuilder.Register(typeof(T));
            return SqlMapper.Query<T>(conn, sql, param, transaction, buffered, commandTimeout, commandType);
        }
    }
}
