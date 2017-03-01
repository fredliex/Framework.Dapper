using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class ModelColumnInfoCollection : IReadOnlyCollection<ModelColumnInfo>
    {
        private ModelColumnInfo[] cols;

        //由member name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> memberMap;
        //由column name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> columnMap;
        //設定為IsConcurrencyCheck的欄位序
        private int? concurrencyCheckColumnIndex = null;

        #region 實作IReadOnlyCollection<ColumnInfo>
        public int Count => cols.Length;
        public IEnumerator<ModelColumnInfo> GetEnumerator() => ((IEnumerable<ModelColumnInfo>)cols).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => cols.GetEnumerator();
        #endregion

        internal ModelColumnInfoCollection(IEnumerable<ModelColumnInfo> columns)
        {
            cols = columns.ToArray();
            memberMap = new Dictionary<string, int>(cols.Length);
            columnMap = new Dictionary<string, int>(cols.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cols.Length; i++)
            {
                var column = cols[i];
                memberMap[column.MemberName] = columnMap[column.ColumnName] = i;
                if (column.IsConcurrencyCheck)
                {
                    if (concurrencyCheckColumnIndex.HasValue) throw new InvalidOperationException("最多只能一個欄位設定ColumnAttribute.IsConcurrencyCheck為True。");
                    concurrencyCheckColumnIndex = i;
                }
            }
        }

        public ModelColumnInfo GetColumn(string columnName)
        {
            int colIndex;
            return columnMap.TryGetValue(columnName, out colIndex) ? cols[colIndex] : null;
        }

        internal static Action<IDictionary<string, object>, object> GenerateDictionaryFiller(Type modelType)
        {
            return ModelColumnInfo.GenerateDictionaryFiller(modelType, ModelTableInfo.Get(modelType).Columns);
        }

    }
}
