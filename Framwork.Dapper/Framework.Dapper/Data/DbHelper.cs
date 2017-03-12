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

        //放置連線字串名稱所對應的 Func<DbConnection>
        private static Hashtable cacheConnectionCreator = new Hashtable();
        public static DbConnectionWrapper OpenConnection(string connName)
        {
            var connCreator = (Func<DbConnectionWrapper>)cacheConnectionCreator[connName];
            if (connCreator == null)
            {
                lock(cacheConnectionCreator)
                {
                    var setting = ConfigurationManager.ConnectionStrings[connName];
                    var connString = setting.ConnectionString;
                    var factory = DbProviderFactories.GetFactory(setting.ProviderName);
                    var factoryWrapper = DbWrapperHelper.Wrap(factory);
                    Func<DbConnectionWrapper> tmpConnCreator = () =>
                    {
                        var newConn = (DbConnectionWrapper)factoryWrapper.CreateConnection();
                        newConn.ConnectionString = connString;
                        return newConn;
                    };
                    connCreator = (Func<DbConnectionWrapper>)cacheConnectionCreator[connName];
                    if (connCreator == null) cacheConnectionCreator[connName] = connCreator = tmpConnCreator;
                }
            }
            var conn = connCreator();
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
                var deserializer = cache.GetOrAddDeserializer(new[] { resultType }, types => new[] { GetDeserializer(types[0], reader) })[0];
                while (reader.Read())
                {
                    yield return (T)deserializer(reader);
                }
            }
        }
        #endregion

        #region return multi type
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, DontMap, DontMap, DontMap, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, DontMap, DontMap, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, DontMap, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, TFifth, DontMap, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, DontMap, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
        {
            return Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(conn, sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        private static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(this IDbConnection conn, string sql, Delegate map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
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
                var resultTypes = new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh) };
                var deserializers = cache.GetOrAddDeserializer(resultTypes, types => GetDeserializers(types, splitOn, reader));
                var mapIt = ModelWrapper.Reflect.Dapper.GenerateMapper<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(deserializers, map);

                if (mapIt != null)
                {
                    while (reader.Read())
                    {
                        yield return mapIt(reader);
                    }
                }
            }
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
