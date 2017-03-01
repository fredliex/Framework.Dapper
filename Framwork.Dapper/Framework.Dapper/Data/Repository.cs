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


    }

    internal class Repository<T>
    {
        private static ModelTableInfo tableInfo = ModelTableInfo.Get(typeof(T));

        private IDbConnection conn;
        protected string DataBase { get; private set; }
        protected string Schema { get; private set; }
        protected string Table { get; private set; }
        private string FullTableName;

        /// <summary>建構式，可指定database、schema、table</summary>
        /// <param name="conn">資料庫連線字串</param>
        /// <param name="dataBase">指定database，null的話依照TableAttribute指示。</param>
        /// <param name="schema">指定schema，null話依照TableAttribute指示。</param>
        /// <param name="table">指定table，null話依照TableAttribute指示。</param>
        public Repository(IDbConnection conn, string dataBase = null, string schema = null, string table = null)
        {
            this.conn = conn;
            DataBase = dataBase ?? tableInfo.Database;
            Schema = schema ?? tableInfo.Schema;
            Table = table ?? tableInfo.Table;

            FullTableName = table;
            if (Schema != null) FullTableName = $"{Schema}.{FullTableName}";
            if (DataBase != null) FullTableName = $"{DataBase}.{FullTableName}";
        }

        #region select
        /// <summary>依照指定條件查詢資料</summary>
        /// <param name="param">查詢條件，null表示查詢任何資料。</param>
        /// <returns></returns>
        public IEnumerable<T> Select(object param = null)
        {
            var metadata = GetSelectMetadata(param == null ? null : new RepositoryMatedata(null, param));
            return conn.Query<T>(metadata.SqlStr, metadata.Param);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        protected virtual RepositoryMatedata GetSelectMetadata(RepositoryMatedata metadata)
        {
            var sqlStr = $"select * from {FullTableName}";
            if (metadata.ParamColumns.Any()) sqlStr += " where " + string.Join(" and ", metadata.ParamColumns.Select(n => $"{n} = @{n}"));
            metadata.SqlStr = sqlStr;
            return metadata;
        }
        #endregion

        #region update
        /// <summary>依照指定條件查詢資料</summary>
        /// <param name="model">新資料</param>
        /// <param name="param">舊資料條件。為避免意外，所以必須指定條件，不得null。</param>
        /// <returns></returns>
        public int Update(T model, object param)
        {
            if (param == null) throw new Exception("Update必須指定舊資料條件");
            var metadata = GetUpdateMetadata(param == null ? null : new RepositoryMatedata(model, param));
            return conn.Execute(metadata.SqlStr, metadata.Param);
        }

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        protected RepositoryMatedata GetUpdateMetadata(RepositoryMatedata metadata)
        {

            TODO

        }
        #endregion
    }
}
