using Dapper;
using System;
using System.Collections;
using System.Collections.Concurrent;
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

        public static IEnumerable<dynamic> Query(this IDbConnection cnn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
        {
            return Query<object>(cnn, sql, param as object, transaction, buffered, commandTimeout, commandType);
        }

        public static IEnumerable<T> Query<T>(this IDbConnection conn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
        {
            var datas = QueryImp<T>(conn, sql, param, transaction, buffered, commandTimeout, commandType ?? CommandType.Text);
            return buffered ? datas.ToList() : datas;
        }

        private static IEnumerable<T> QueryImp<T>(this IDbConnection conn, string sql, object param, IDbTransaction transaction, bool buffered, int? commandTimeout, CommandType commandType)
        {
            ModelWrapper.Cache cache;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, commandType, sql, out cache);
            var commandDefinition = new CommandDefinition(sql, paramWrapper, transaction, commandTimeout, commandType, CommandFlags.Buffered);
            using (var reader = SqlMapper.ExecuteReader(conn, commandDefinition, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
            {
                var resultType = typeof(T);
                var typeDeserializer = cache.GetOrAddDeserializer(new[] { resultType }, x => ModelWrapper.DeserializerBuilder.GetDeserializer(resultType, reader));
                while (reader.Read())
                {
                    yield return (T)typeDeserializer(reader);
                }
            }
        }



        public static int Execute(this IDbConnection conn, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            var cmdType = commandType ?? CommandType.Text;
            ModelWrapper.Cache cache;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, cmdType, sql, out cache);
            var commandDefinition = new CommandDefinition(sql, paramWrapper, transaction, commandTimeout, cmdType, CommandFlags.Buffered);
            return SqlMapper.Execute(conn, commandDefinition);
        }
    }
}
