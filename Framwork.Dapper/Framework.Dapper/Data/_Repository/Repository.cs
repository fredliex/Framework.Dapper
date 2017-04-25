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
        public static Repository<T> GetRepository<T>(this IDbConnection conn, RepositoryOption? option = null) => new Repository<T>(conn, option);

        #region select
        /// <summary>透過Repository查詢全部資料</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(IDbConnection conn) where T : IDbModel => 
            GetRepository<T>(conn).Select();

        /// <summary>透過Repository查詢特定資料</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(object filter, IDbConnection conn) where T : IDbModel =>
            GetRepository<T>(conn).Select(filter);
        #endregion

        #region insert
        /// <summary>透過Repository將model新增</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Insert<T>(this T model, IDbConnection conn) where T : IDbModel => 
            GetRepository<T>(conn).Insert(model);

        /// <summary>透過Repository將多筆model新增</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="models"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Inserts<T>(this IEnumerable<T> models, IDbConnection conn) where T : IDbModel =>
            GetRepository<T>(conn).Inserts(models);
        #endregion

        #region update
        /// <summary>透過Repository將符合filter的資料更新為model。通常用於未標示key或鍵值有變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Update<T>(this T model, object filter, IDbConnection conn) where T : IDbModel => 
            GetRepository<T>(conn).Update(filter, model);

        /// <summary>透過Repository將符合model更新回資料庫。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Update<T>(this T model, IDbConnection conn) where T : IDbModel => 
            GetRepository<T>(conn).Update(model);

        /// <summary>透過Repository將多筆model更新回資料庫。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="models"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Updates<T>(this IEnumerable<T> models, IDbConnection conn) where T : IDbModel =>
            GetRepository<T>(conn).Updates(models);
        #endregion

        #region delete
        /// <summary>透過Repository將符合filter的資料刪除。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Delete<T>(object filter, IDbConnection conn) where T : IDbModel => 
            GetRepository<T>(conn).Delete(filter);

        /// <summary>透過Repository將model自資料庫中刪除。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Delete<T>(this T model, IDbConnection conn) where T : IDbModel =>
            GetRepository<T>(conn).Delete(model);

        /// <summary>透過Repository將model自資料庫中刪除。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Deletes<T>(this IEnumerable<T> models, IDbConnection conn) where T : IDbModel =>
            GetRepository<T>(conn).Deletes(models);
        #endregion

        #region merge save
        public static int Save<T>(this ModelMerger<T> merger, IDbConnection conn) where T : IDbModel => 
            merger.Delete.Deletes(conn) + merger.Update.Updates(conn) + merger.Insert.Inserts(conn);
        #endregion
    }

}
