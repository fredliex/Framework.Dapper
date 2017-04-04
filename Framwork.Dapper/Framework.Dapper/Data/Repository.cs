using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    public static class Repository
    {
        private static Repository<T> GetRepository<T>(IDbConnection conn, RepositoryOption? option) => new Repository<T>(conn, option);

        #region select
        /// <summary>透過Repository查詢全部資料</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(IDbConnection conn, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Select(null);

        /// <summary>透過Repository查詢特定資料</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(IDbConnection conn, object filter, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Select(filter);

        /// <summary>透過Repository查詢特定資料</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(IDbConnection conn, T filter, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Select(filter);
        #endregion

        #region insert
        /// <summary>透過Repository將model新增</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Insert<T>(this T model, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Insert(model);

        /// <summary>透過Repository將多筆model新增</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="models"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Inserts<T>(this IEnumerable<T> models, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel =>
            GetRepository<T>(conn, option).Inserts(models);
        #endregion

        #region update
        /// <summary>透過Repository將符合filter的資料更新為model。通常用於未標示key或鍵值有變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Update<T>(this T model, IDbConnection conn, object filter, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Update(filter, model);

        /// <summary>透過Repository將符合model更新回資料庫。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Update<T>(this T model, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Update(model, model);

        /// <summary>透過Repository將多筆model更新回資料庫。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="models"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Updates<T>(this IEnumerable<T> models, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel =>
            GetRepository<T>(conn, option).Updates(models);
        #endregion

        #region delete
        /// <summary>透過Repository將符合filter的資料刪除。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Delete<T>(IDbConnection conn, object filter, RepositoryOption? option = null) where T : IDataModel => 
            GetRepository<T>(conn, option).Delete(filter);

        /// <summary>透過Repository將model自資料庫中刪除。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Delete<T>(this T model, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel =>
            GetRepository<T>(conn, option).Delete(model);

        /// <summary>透過Repository將model自資料庫中刪除。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Deletes<T>(this IEnumerable<T> models, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel =>
            GetRepository<T>(conn, option).Deletes(models);
        #endregion
    }

}
