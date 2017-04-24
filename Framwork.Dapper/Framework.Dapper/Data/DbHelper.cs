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
        public static IEnumerable<dynamic> Query(this IDbConnection conn, string sql, object param = null, CommandOption? option = null) => Query<object>(conn, null, sql, param, option);

        public static IEnumerable<T> Query<T>(this IDbConnection conn, string sql, object param = null, CommandOption? option = null) => Query<T>(conn, null, sql, param, option);

        public static IEnumerable<dynamic> Query(this IDbTransaction trans, string sql, object param = null, CommandOption? option = null) => Query<object>(null, trans, sql, param, option);

        public static IEnumerable<T> Query<T>(this IDbTransaction trans, string sql, object param = null, CommandOption? option = null) => Query<T>(null, trans, sql, param, option);

        private static IEnumerable<T> Query<T>(IDbConnection conn, IDbTransaction trans, string sql, object param = null, CommandOption? option = null)
        {
            var datas = QueryImp<T>(conn, trans, sql, param, option);
            return option?.Buffered == false ? datas : datas.ToList();
        }

        private static IEnumerable<T> QueryImp<T>(IDbConnection conn, IDbTransaction trans, string sql, object param, CommandOption? option = null)
        {
            if (conn == null) conn = trans.Connection;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, option?.CommandType ?? CommandType.Text, sql, out var cache);
            var commandDefinition = (option ?? new CommandOption()).ToDefinition(sql, paramWrapper, trans);
            using (var reader = SqlMapper.ExecuteReader(conn, commandDefinition, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
            {
                var resultType = typeof(T);
                var deserializer = cache.GetDeserializer(new[] { resultType }, types => new[] { GetDeserializer(types[0], reader) })[0];
                while (reader.Read())
                {
                    yield return (T)deserializer(reader);
                }
            }
        }
        #endregion

        #region return multi type

        #region IDbConnection
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TReturn> map, 
            object param = null, string splitOn = "Id", CommandOption? option = null) => 
            Query<TFirst, TSecond, DontMap, DontMap, DontMap, DontMap, DontMap, TReturn>(conn, null, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, DontMap, DontMap, DontMap, DontMap, TReturn>(conn, null, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, DontMap, DontMap, DontMap, TReturn>(conn, null, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, TFifth, DontMap, DontMap, TReturn>(conn, null, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, DontMap, TReturn>(conn, null, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(this IDbConnection conn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(conn, null, sql, map, param, splitOn, option);
        #endregion

        #region IDbTransaction
        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this IDbTransaction trans, string sql, Func<TFirst, TSecond, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, DontMap, DontMap, DontMap, DontMap, DontMap, TReturn>(null, trans, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this IDbTransaction trans, string sql, Func<TFirst, TSecond, TThird, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, DontMap, DontMap, DontMap, DontMap, TReturn>(null, trans, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbTransaction trans, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, DontMap, DontMap, DontMap, TReturn>(null, trans, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbTransaction trans, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, TFifth, DontMap, DontMap, TReturn>(null, trans, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(this IDbTransaction trans, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, DontMap, TReturn>(null, trans, sql, map, param, splitOn, option);

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(this IDbTransaction trans, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map,
            object param = null, string splitOn = "Id", CommandOption? option = null) =>
            Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(null, trans, sql, map, param, splitOn, option);
        #endregion


        private static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(IDbConnection conn, IDbTransaction trans, string sql, Delegate map, object param, string splitOn, CommandOption? option = null)
        {
            var datas = QueryImp<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(conn, trans, sql, map, param, splitOn, option);
            return option?.Buffered == false ? datas : datas.ToList();
        }

        private static IEnumerable<TReturn> QueryImp<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(IDbConnection conn, IDbTransaction trans, string sql, Delegate map, object param, string splitOn, CommandOption? option = null)
        {
            if (conn == null) conn = trans.Connection;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, option?.CommandType ?? CommandType.Text, sql, out var cache);
            var commandDefinition = (option ?? new CommandOption()).ToDefinition(sql, paramWrapper, trans);
            using (var reader = SqlMapper.ExecuteReader(conn, commandDefinition, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
            {
                var resultTypes = new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh) };
                var deserializers = cache.GetDeserializer(resultTypes, types => GetDeserializers(types, splitOn, reader));
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
        public static int Execute(this IDbConnection conn, string sql, object param = null, CommandOption? option = null) =>
            Execute(conn, null, sql, param, option);

        public static int Execute(this IDbTransaction trans, string sql, object param = null, CommandOption? option = null) =>
            Execute(null, trans, sql, param, option);

        private static int Execute(IDbConnection conn, IDbTransaction trans, string sql, object param = null, CommandOption? option = null)
        {
            if (conn == null) conn = trans.Connection;
            var paramWrapper = ModelWrapper.WrapParam(conn, param, option?.CommandType ?? CommandType.Text, sql, out var cache);
            var commandDefinition = (option ?? new CommandOption()).ToDefinition(sql, paramWrapper, trans);
            return SqlMapper.Execute(conn, commandDefinition);
        }
        #endregion
    }
}
