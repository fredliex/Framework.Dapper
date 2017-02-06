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
    internal sealed class TableInfo
    {
        private static ConcurrentDictionary<Type, TableInfo> cache = new ConcurrentDictionary<Type, TableInfo>();

        /// <summary>取得Table資訊</summary>
        internal static TableInfo Get(Type modelType)
        {
            return cache.GetOrAdd(modelType, t => new TableInfo(t));
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

        /// <summary>是否有繼承IModel</summary>
        internal bool HasModelInterface { get; private set; }

        /// <summary>欄位資訊</summary>
        public ColumnInfoCollection Columns { get; private set; }

        private Action<IDictionary<string, object>, object> paremterFiller = null;
        internal Action<IDictionary<string, object>, object> ParemterFiller => paremterFiller ?? (paremterFiller = GenerateParemterFiller());

        private TableInfo(Type modelType)
        {
            Type = modelType;
            IsStructModel = modelType.IsValueType;
            HasModelInterface = typeof(IModel).IsAssignableFrom(modelType);

            //有繼承IModel就看TableAttribute，TableAttribute忽略繼承鍊。
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

            Columns = new ColumnInfoCollection(Type, HasModelInterface, IsStructModel);
        }

        private static MethodInfo dictionarySetMethod = typeof(IDictionary<string, object>).GetProperties().First(p => p.GetIndexParameters().Length > 0).GetSetMethod();
        private Action<IDictionary<string, object>, object> GenerateParemterFiller()
        {
            //Key為ColumnName
            var expParamDict = Expression.Parameter(typeof(IDictionary<string, object>));
            var expParamObject = Expression.Parameter(typeof(object));
            var expVarModel = Expression.Variable(Type);
            var expBody = new List<Expression>();
            expBody.Add(Expression.Assign(expVarModel, Expression.Convert(expParamObject, Type)));
            foreach (var column in Columns)
            {
                var member = column.Member;
                var expValue = member.MemberType == MemberTypes.Field ? Expression.Field(expVarModel, (FieldInfo)member) : Expression.Property(expVarModel, (PropertyInfo)member);



                expBody.Add(Expression.Call(expParamDict, dictionarySetMethod, Expression.Constant(column.ColumnName), expValue));
            }
            var expBlock = Expression.Block(new[] { expVarModel }, expBody);
            var lambda = Expression.Lambda<Action<IDictionary<string, object>, object>>(expBlock, new[] { expParamDict, expParamObject });
            return lambda.Compile();
        }

        /*
        internal RepositoryMatedata GetRepositoryMatedata(object data)
        {
            return new RepositoryMatedata(this, data);
        }
        */
    }
}
