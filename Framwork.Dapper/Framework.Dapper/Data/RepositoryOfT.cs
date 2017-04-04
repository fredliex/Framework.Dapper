using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
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

        /// <summary>將多個model新增</summary>
        /// <param name="models">多個model</param>
        /// <returns>異動筆數</returns>
        public int Inserts(IEnumerable<T> models)
        {
            //以第一個model組出來的sql字串重複執行
            var firstModel = models.FirstOrDefault();
            if (firstModel == null) throw new ArgumentNullException("model不可為null");
            var metadata = GetInsertMetadata(new RepositoryMatedata(firstModel, null));
            return conn.Execute(metadata.SqlStr, models);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        private RepositoryMatedata GetInsertMetadata(RepositoryMatedata metadata)
        {
            var sqlFields = new List<string>();
            var sqlValues = new List<string>();
            foreach (var col in tableInfo.Columns)
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
        /// <param name="filter">舊資料條件。為避免意外，所以必須指定條件，不得null。</param>
        /// <param name="model">新資料</param>
        /// <returns></returns>
        public int Update(object filter, T model)
        {
            if (filter == null) throw new ArgumentNullException("filter不可為null");
            if (model == null) throw new ArgumentNullException("model不可為null");
            var metadata = GetUpdateMetadata(new RepositoryMatedata(model, filter));
            return conn.Execute(metadata.SqlStr, metadata.Param);
        }

        /// <summary>更新多個model</summary>
        /// <param name="models">多個model</param>
        /// <returns>異動筆數</returns>
        public int Updates(IEnumerable<T> models)
        {
            //以第一個model組出來的sql字串重複執行
            var firstModel = models.FirstOrDefault();
            if (firstModel == null) throw new ArgumentNullException("model不可為null");
            var metadata = GetUpdateMetadata(new RepositoryMatedata(firstModel, firstModel));
            //因為這邊values和filter都是model本身, 所以不需要額外參數載具
            return conn.Execute(metadata.SqlStr, models);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns>產生的sql為 update {model定義的Table} set {model資料} where {filter條件}</returns>
        private RepositoryMatedata GetUpdateMetadata(RepositoryMatedata metadata)
        {
            string sqlFilter; //查詢條件sql 
            metadata.Param = metadata.Filter;
            IEnumerable<ModelColumnInfo> valueColumns = tableInfo.Columns; //new欄位值
            if (object.ReferenceEquals(metadata.Model, metadata.Filter)) //filter就是model本身
            {
                var excludeValues = new HashSet<string>(); //model同filter時，須排除已經出現於條件的欄位
                sqlFilter = GetFilterSection(metadata, true, col =>
                {
                    if (!col.IsConcurrencyCheck) excludeValues.Add(col.ColumnName);
                    return col.MemberName;
                });
                if (excludeValues.Count > 0) valueColumns = valueColumns.Where(n => !excludeValues.Contains(n.ColumnName)); //排除已經出現於filter的欄位
            }
            else
            {
                var renameFilters = new Dictionary<string, string>(); //會被改名的條件欄位, key是ColumnName, value是改名後的MemberName
                sqlFilter = GetFilterSection(metadata, true, col => renameFilters[col.ColumnName] = "_old_" + col.MemberName);
                if (renameFilters.Count > 0)
                {
                    var oldValues = DataModelHelper.ToDictionary(metadata.Filter);  //全部條件值
                    var newParam = new DynamicParameters(metadata.Model); //建立新的參數載具, 以放置model以及filter的資料
                    foreach (var n in renameFilters) newParam.Add(n.Value, oldValues[n.Key]);
                    metadata.Param = newParam;
                }
            }
            
            var sqlStr = $"update {FullTableName} set ";
            sqlStr += string.Join(",", valueColumns.Select(n => $"{n.ColumnName}={GetValueParameterSection(n)}"));
            if (sqlFilter != null) sqlStr += sqlFilter;

            metadata.SqlStr = sqlStr;
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

        /// <summary>更新多個model</summary>
        /// <param name="models">多個model</param>
        /// <returns>異動筆數</returns>
        public int Deletes(IEnumerable<T> models)
        {
            //以第一個model組出來的sql字串重複執行
            var firstModel = models.FirstOrDefault();
            if (firstModel == null) throw new ArgumentNullException("model不可為null");
            var metadata = GetDeleteMetadata(new RepositoryMatedata(null, firstModel));
            return conn.Execute(metadata.SqlStr, models);
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
            //TODO: need detect dbProviderType to change getdate and sysdatetimeoffset
            return
                !column.IsConcurrencyCheck ? "@" + column.MemberName :
                column.ElementUnderlyingType == typeof(DateTime) ? "getdate()" :
                column.ElementUnderlyingType == typeof(DateTimeOffset) ? "sysdatetimeoffset()" :
                throw new Exception("IsConcurrencyCheck無法應用於非DateTime或DateTimeOffset欄位");
        }

        /// <summary>取得答詢條件的sql片段</summary>
        /// <param name="metadata"></param>
        /// <param name="includeConcurrencyField">是否要IsConcurrencyCheck也列入查詢條件。</param>
        /// <param name="parameterNameGetter">用以取得參數名稱。null的話表示依照MemberName。</param>
        /// <returns>傳回含有where的sql片段</returns>
        private static string GetFilterSection(RepositoryMatedata metadata, bool includeConcurrencyField, Func<ColumnInfo, string> parameterNameGetter)
        {
            if (metadata.FilterColumns != null)
            {
                //如果參數沒有定義Iskey或IsConcurrencyCheck的話，代表所有欄位都是條件
                var filterBehavior = includeConcurrencyField ? col => col.IsKey || col.IsConcurrencyCheck : new Func<ColumnInfo, bool>(col => col.IsKey);
                var filterColumns = metadata.FilterColumns.Where(filterBehavior).ToList();
                if (filterColumns.Count == 0) filterColumns = metadata.FilterColumns.ToList();
                var filters = filterColumns.Select(n =>
                    string.Format("{0}{1}@{2}", n.ColumnName, n.IsMultiValue ? " in " : "=", parameterNameGetter == null ? n.MemberName : parameterNameGetter(n))
                ).ToList();
                if (filters.Count > 0) return " where " + string.Join(" and ", filters);
            }
            return null;
        }
    }
}
