using Dapper;
using System;
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

        public static IEnumerable<T> Query<T>(this IDbConnection conn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
        {
            //依照commandType + sql + conn + param.GetType() + T 當作識別key
            var identity = ModelWrapper.Reflect.Dapper.NewIdentity(sql, commandType, conn, typeof(T), param?.GetType(), null);


            var paramWrapper = ModelWrapper.WrapParam(param, commandType ?? CommandType.Text, sql);
            var commandDefinition = new CommandDefinition(sql, paramWrapper, transaction, commandTimeout, commandType, CommandFlags.Buffered);
            using (var reader = SqlMapper.ExecuteReader(conn, commandDefinition, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
            {
                var deserializerBuilder = new ModelWrapper.DeserializerBuilder();
                var typeDeserializer = deserializerBuilder.GetDeserializer(typeof(T), reader);

                var buff = new List<T>();
                while (reader.Read())
                {
                    buff.Add((T)typeDeserializer(reader));
                }
                return buff;
            }
        }
    }
}
