using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class ColumnInfoCollection : IReadOnlyCollection<ColumnInfo>
    {
        private ColumnInfo[] cols;

        //由member name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> memberMap;
        //由column name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> columnMap;
        //設定為IsConcurrencyCheck的欄位序
        private int? concurrencyCheckColumnIndex = null;

        public int Count
        {
            get { return cols.Length; }
        }

        internal ColumnInfoCollection(Type modelType, bool? hasModelMark, bool? isStructType)
        {

        }

        internal ColumnInfoCollection(IEnumerable<ColumnInfo> columns)
        {
            cols = columns.ToArray();
            memberMap = new Dictionary<string, int>(cols.Length);
            columnMap = new Dictionary<string, int>(cols.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cols.Length; i++)
            {
                var column = cols[i];
                memberMap[column.MemberName] = columnMap[column.Name] = i;
                if (column.IsConcurrencyCheck)
                {
                    if (concurrencyCheckColumnIndex.HasValue) throw new InvalidOperationException("最多只能一個欄位設定ColumnAttribute.IsConcurrencyCheck為True。");
                    concurrencyCheckColumnIndex = i;
                }
            }
        }

        public ColumnInfo GetColumn(string columnName)
        {
            int colIndex;
            return columnMap.TryGetValue(columnName, out colIndex) ? cols[colIndex] : null;
        }

        public IEnumerator<ColumnInfo> GetEnumerator()
        {
            return ((IEnumerable<ColumnInfo>)cols).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return cols.GetEnumerator();
        }

    }
}
