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
        private static TableInfo tableInfo = TableInfo.Get(typeof(T));

        private static List<RepositoryMatedata> GetMatedatas(object data)
        {
            var multiData = data as IEnumerable;
            IEnumerable<object> datas;
            if (multiData != null && !(multiData is IEnumerable<KeyValuePair<string, object>>))
            {
                if (multiData is string) throw new ArgumentException("data不可為字串");
                datas = multiData.Cast<object>().Where(x => x != null);
            }
            else
            {
                datas = new[] { data };
            }
            return null;
            //return datas.Select(tableInfo.GetRepositoryMatedata).ToList();
        }

        private IDbConnection conn;
        protected string DataBase { get; private set; }
        protected string Schema { get; private set; }
        protected string Table { get; private set; }

        /// <summary>建構式，可指定database、schema、table</summary>
        /// <param name="conn">資料庫連線字串</param>
        /// <param name="dataBase">指定database，不指定的話依照TableAttribute指示。</param>
        /// <param name="schema">指定schema，不指定的話依照TableAttribute指示。</param>
        /// <param name="table">指定table，不指定的話依照TableAttribute指示。</param>
        public Repository(IDbConnection conn, string dataBase = null, string schema = null, string table = null)
        {
            this.conn = conn;
            DataBase = dataBase ?? tableInfo.Database;
            Schema = schema ?? tableInfo.Schema;
            Table = table ?? tableInfo.Table;
        }

        #region select
        /*
        public IEnumerable<T> Select(object parameters = null)
        {

        }


        protected virtual IEnumerable<T> SelectCore(string sql, ParameterMatedata columns)
        {

        }
        */

        /// <summary>依照matedata來產生sql</summary>
        /// <param name="matedata"></param>
        /// <returns></returns>
        protected virtual string GetSelectSql(MemberColumnInfoCollection columns)
        {
            var sqlStr = $"select * from {Table}";
            //if(parameters != null)
            throw new NotImplementedException();
        }
        #endregion


    }
}
