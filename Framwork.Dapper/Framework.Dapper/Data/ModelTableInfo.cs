using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class ModelTableInfo
    {
        private static ConcurrentDictionary<Type, ModelTableInfo> cache = new ConcurrentDictionary<Type, ModelTableInfo>();

        /// <summary>取得Table資訊</summary>
        internal static ModelTableInfo Get(Type modelType)
        {
            return cache.GetOrAdd(modelType, t => new ModelTableInfo(t));
        }

        /// <summary>資料庫名稱</summary>
        public string Database { get; private set; }

        /// <summary>Schema名稱</summary>
        public string Schema { get; private set; }

        /// <summary>Table名稱</summary>
        public string Table { get; private set; }

        /// <summary>Model型別</summary>
        internal Type Type { get; private set; }

        /// <summary>Model是否為struct</summary>
        internal bool IsStructModel { get; private set; }

        /// <summary>是否有繼承IDataModel</summary>
        internal bool HasModelInterface { get; private set; }

        /// <summary>欄位資訊</summary>
        public ModelColumnInfoCollection Columns { get; private set; }

        private ModelTableInfo(Type modelType)
        {
            Type = modelType;
            IsStructModel = modelType.IsValueType;
            HasModelInterface = typeof(IDataModel).IsAssignableFrom(modelType);

            //有繼承IDataModel就看TableAttribute，TableAttribute忽略繼承鍊。
            var attr = HasModelInterface ? modelType.GetAttribute<TableAttribute>(false) : null;
            if (attr != null)
            {
                Database = attr.Database;
                Schema = attr.Schema;
                Table = attr.Name;
            }
            if (string.IsNullOrWhiteSpace(Database)) Database = null;
            if (string.IsNullOrWhiteSpace(Schema)) Schema = null;
            if (string.IsNullOrWhiteSpace(Table)) Table = modelType.Name;

            Columns = new ModelColumnInfoCollection(ModelColumnInfo.Resolve(Type, HasModelInterface, IsStructModel));
        }
    }
}
