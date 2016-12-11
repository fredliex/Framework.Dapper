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
using static Framework.Data.ModelWrapper.DeserializerBuilder;

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

        #region return single type
        public static IEnumerable<dynamic> Query(this IDbConnection cnn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
        {
            return Query<object>(cnn, sql, param as object, transaction, buffered, commandTimeout, commandType);
        }

        public static IEnumerable<T> Query<T>(this IDbConnection conn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
        {
            var datas = QueryImp<T>(conn, sql, param, transaction, commandTimeout, commandType ?? CommandType.Text);
            return buffered ? datas.ToList() : datas;
        }

        private static IEnumerable<T> QueryImp<T>(this IDbConnection conn, string sql, object param, IDbTransaction transaction, int? commandTimeout, CommandType commandType)
        {
            ModelWrapper.Cache cache;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, commandType, sql, out cache);
            var commandDefinition = new CommandDefinition(sql, paramWrapper, transaction, commandTimeout, commandType, CommandFlags.Buffered);
            using (var reader = SqlMapper.ExecuteReader(conn, commandDefinition, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
            {
                var resultType = typeof(T);
                var typeDeserializer = cache.GetOrAddDeserializer(new[] { resultType }, x => GetDeserializer(resultType, reader));
                while (reader.Read())
                {
                    yield return (T)typeDeserializer(reader);
                }
            }
        }
        #endregion

        #region return multi type
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, DontMap, DontMap, DontMap, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, DontMap, DontMap, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, DontMap, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, TFifth, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            var datas = QueryImp<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(conn, sql, map, param, transaction, splitOn, commandTimeout, commandType ?? CommandType.Text);
            return buffered ? datas.ToList() : datas;
        }

        private static IEnumerable<TReturn> QueryImp<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, string splitOn, int? commandTimeout, CommandType commandType)
        {
            ModelWrapper.Cache cache;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, commandType, sql, out cache);
            var commandDefinition = new CommandDefinition(sql, paramWrapper, transaction, commandTimeout, commandType, CommandFlags.Buffered);
            using (var reader = SqlMapper.ExecuteReader(conn, commandDefinition, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
            {
                
            }
            return null;
        }
        #endregion

        #region execute
        public static int Execute(this IDbConnection conn, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            var cmdType = commandType ?? CommandType.Text;
            ModelWrapper.Cache cache;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, cmdType, sql, out cache);
            var commandDefinition = new CommandDefinition(sql, paramWrapper, transaction, commandTimeout, cmdType, CommandFlags.Buffered);
            return SqlMapper.Execute(conn, commandDefinition);
        }
        #endregion
    }
}
