using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class RepositoryMatedata
    {
        internal List<ColumnInfo> Columns { get; private set; }

        public RepositoryMatedata(object data, TableInfo tableInfo)
        {
            if (data == null) Columns = new List<ColumnInfo>();

            var type = data.GetType();

            if (data is IEnumerable<KeyValuePair<string, object>>)
            {
                if (data is string) throw new ArgumentException("data不可為字串");


            }

                if (tableInfo == null) tableInfo = TableInfo.Get(type);
            










        }
    }
}
