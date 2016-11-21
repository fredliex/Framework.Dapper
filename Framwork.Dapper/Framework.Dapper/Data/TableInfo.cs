using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class TableInfo
    {
        /// <summary>資料庫名稱</summary>
        public string Database { get; private set; }

        /// <summary>Schema名稱</summary>
        public string Schema { get; private set; }

        /// <summary>Table名稱</summary>
        public string Table { get; private set; }

        /// <summary>欄位資訊</summary>
        public ColumnInfo[] Columns { get; private set; }

        internal Type Type { get; private set; }
        internal bool IsStruct { get; private set; }
        internal bool IsModel { get; private set; }


        //由member name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> memberMap;
        //由column name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> columnMap;
        //設定為IsConcurrencyCheck的欄位序
        private int? concurrencyCheckColumnIndex = null;

        internal TableInfo(Type modelType)
        {
            Type = modelType;
            IsStruct = modelType.IsValueType;
            IsModel = typeof(IModel).IsAssignableFrom(modelType);

            //有繼承IModel就看TableAttribute，TableAttribute忽略繼承鍊。
            var attr = IsModel ? modelType.GetAttribute<TableAttribute>(false) : null;
            if (attr != null)
            {
                Database = attr.Database;
                Schema = attr.Schema;
                Table = attr.Name;
            }
            if (string.IsNullOrWhiteSpace(Database)) Database = null;
            if (string.IsNullOrWhiteSpace(Schema)) Schema = null;
            if (string.IsNullOrWhiteSpace(Table)) Table = modelType.Name;

            InitColumns();
        }


        private void InitColumns()
        {
            Columns = ColumnInfo.Resolve(Type, IsModel, IsStruct).ToArray();
            memberMap = new Dictionary<string, int>(Columns.Length);
            columnMap = new Dictionary<string, int>(Columns.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < Columns.Length; i++)
            {
                var column = Columns[i];
                memberMap[column.MemberName] = columnMap[column.Name] = i;
                if (column.IsConcurrencyCheck)
                {
                    if (concurrencyCheckColumnIndex.HasValue) throw new InvalidOperationException("最多只能一個欄位設定ColumnAttribute.IsConcurrencyCheck為True。");
                    concurrencyCheckColumnIndex = i;
                }
            }
        }

        internal ColumnInfo GetColumn(string columnName)
        {
            int colIndex;
            return columnMap.TryGetValue(columnName, out colIndex) ? Columns[colIndex] : null;
        }
    }
}
