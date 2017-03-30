using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Framework.Data.Repository;

namespace Framework.Data
{
    public static partial class Repository
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
        public static IEnumerable<T> Select<T>(IDbConnection conn, object filter, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Select(filter);

        /// <summary>透過Repository查詢特定資料</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(IDbConnection conn, T filter, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Select(filter);
        #endregion

        #region insert
        /// <summary>透過Repository將model新增</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Insert<T>(this T model, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Insert(model);

        public static int Inserts<T>(this IEnumerable<T> models, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel
        {
            return 0;
        }
        #endregion

        #region update
        /// <summary>透過Repository將符合filter的資料更新為model。通常用於未標示key或鍵值有變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Update<T>(this T model, IDbConnection conn, object filter, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Update(filter, model);

        /// <summary>透過Repository將符合model更新回資料庫。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Update<T>(this T model, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Update(model, model);
        #endregion

        #region delete
        /// <summary>透過Repository將model自資料庫中刪除。通常用於有標示Key且鍵值不變的情況。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="conn"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Delete<T>(this T model, IDbConnection conn, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Delete(model);

        /// <summary>透過Repository將符合filter的資料刪除。</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="conn"></param>
        /// <param name="filter"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static int Delete<T>(IDbConnection conn, object filter, RepositoryOption? option = null) where T : IDataModel => GetRepository<T>(conn, option).Delete(filter);
        #endregion
    }

    public class Repository<T> : IRepository
    {
        private static ModelTableInfo tableInfo = ModelTableInfo.Get(typeof(T));

        private IDbConnection conn;
        protected RepositoryOption? Option { get; private set; }
        private string FullTableName;

        /// <summary>建構式，可指定database、schema、table</summary>
        /// <param name="conn">資料庫連線字串</param>
        /// <param name="option">參數</param>
        public Repository(IDbConnection conn, RepositoryOption? option = null)
        {
            this.conn = conn;
            Option = option;

            var database = option?.Database ?? tableInfo.Database;
            var schema = option?.Schema ?? tableInfo.Schema;
            var table = option?.Table ?? tableInfo.Table;
            FullTableName = table;
            if (schema != null) FullTableName = $"{schema}.{FullTableName}";
            if (database != null) FullTableName = $"{database}.{FullTableName}";
        }

        #region select
        /// <summary>依照指定條件查詢資料</summary>
        /// <param name="param">查詢條件，null表示查詢任何資料。</param>
        /// <param name="bufferResult">是否將結果全部暫存於記憶體。若處理大量資料時不建議暫存。</param>
        /// <returns></returns>
        public IEnumerable<T> Select(object filter = null, bool buffered = true)
        {
            var metadata = GetSelectMetadata(new RepositoryMatedata(null, filter));
            return conn.Query<T>(metadata.SqlStr, metadata.Param, buffered: buffered);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        private RepositoryMatedata GetSelectMetadata(RepositoryMatedata metadata)
        {
            metadata.SqlStr = $"select * from {FullTableName}{GetFilterSection(metadata, false, null)}";
            metadata.Param = metadata.Filter;
            return metadata;
        }
        #endregion

        #region insert
        /// <summary>將model新增</summary>
        /// <param name="model">model。</param>
        /// <returns>異動筆數</returns>
        public int Insert(T model)
        {
            if (model == null) throw new ArgumentNullException("model不可為null");
            var metadata = GetInsertMetadata(new RepositoryMatedata(model, null));
            return conn.Execute(metadata.SqlStr, metadata.Param);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        private RepositoryMatedata GetInsertMetadata(RepositoryMatedata metadata)
        {
            var sqlFields = new List<string>();
            var sqlValues = new List<string>();
            foreach(var col in tableInfo.Columns)
            {
                sqlFields.Add(col.ColumnName);
                sqlValues.Add(GetValueParameterSection(col));
            }
            var sqlStr = $"insert into {FullTableName} ({string.Join(",", sqlFields)}) values ({string.Join(",", sqlValues)})";
            metadata.SqlStr = sqlStr;
            metadata.Param = metadata.Model;
            return metadata;
        }
        #endregion

        #region update
        /// <summary>依照指定條件查詢資料</summary>
        /// <param name="model">新資料</param>
        /// <param name="param">舊資料條件。為避免意外，所以必須指定條件，不得null。</param>
        /// <returns></returns>
        public int Update(object filter, T model)
        {
            if (filter == null) throw new ArgumentNullException("filter不可為null");
            if (model == null) throw new ArgumentNullException("model不可為null");
            var metadata = GetUpdateMetadata(new RepositoryMatedata(model, filter));
            return conn.Execute(metadata.SqlStr, metadata.Param);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        private RepositoryMatedata GetUpdateMetadata(RepositoryMatedata metadata)
        {
            var filterValues = DataModelHelper.ToDictionary(metadata.Filter);
            var newParam = new DynamicParameters(metadata.Model);
            var sqlFilter = GetFilterSection(metadata, true, col =>
            {
                var paramName = "_key_" + col.MemberName;
                newParam.Add(paramName, filterValues[col.ColumnName]);
                return paramName;
            });
            var sqlStr = $"update {FullTableName} set ";
            sqlStr += string.Join(",", tableInfo.Columns.Select(n => $"{n.ColumnName}={GetValueParameterSection(n)}"));
            if (sqlFilter != null) sqlStr += sqlFilter;
            
            metadata.SqlStr = sqlStr;
            metadata.Param = newParam;
            return metadata;
        }
        #endregion

        #region delete
        /// <summary>依照指定條件查詢資料</summary>
        /// <param name="model">新資料</param>
        /// <param name="filter">舊資料條件。為避免意外，所以必須指定條件，不得null。</param>
        /// <returns></returns>
        public int Delete(object filter)
        {
            if (filter == null) throw new ArgumentNullException("filter不可為null");
            var metadata = GetDeleteMetadata(new RepositoryMatedata(null, filter));
            return conn.Execute(metadata.SqlStr, metadata.Param);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        private RepositoryMatedata GetDeleteMetadata(RepositoryMatedata metadata)
        {
            metadata.SqlStr = $"delete from {FullTableName}{GetFilterSection(metadata, true, null)}";
            metadata.Param = metadata.Filter;
            return metadata;
        }
        #endregion

        private static string GetValueParameterSection(ModelColumnInfo column)
        {
            return
                !column.IsConcurrencyCheck ? "@" + column.MemberName :
                column.ElementUnderlyingType == typeof(DateTime) ? "getdate()" :
                column.ElementUnderlyingType == typeof(DateTimeOffset) ? "sysdatetimeoffset()" :
                throw new Exception("IsConcurrencyCheck無法應用於非DateTime或DateTimeOffset欄位");
        }

        private static string GetFilterSection(RepositoryMatedata metadata, bool includeConcurrencyField, Func<ColumnInfo, string> memberNameGetter)
        {
            if (metadata.FilterColumns != null)
            {
                //如果參數沒有定義Iskey或IsConcurrencyCheck的話，代表所有欄位都是條件
                var filterBehavior = includeConcurrencyField ? col => col.IsKey || col.IsConcurrencyCheck : new Func<ColumnInfo, bool>(col => col.IsKey);
                var filterColumns = metadata.FilterColumns.Where(filterBehavior).ToList();
                if (filterColumns.Count == 0) filterColumns = metadata.FilterColumns.ToList();
                var filters = filterColumns.Select(n =>
                    string.Format("{0}{1}@{2}", n.ColumnName, n.IsMultiValue ? " in " : "=", memberNameGetter == null ? n.MemberName : memberNameGetter(n))
                ).ToList();
                if (filters.Count > 0) return " where " + string.Join(" and ", filters);
            }
            return null;
        }

    }
}
