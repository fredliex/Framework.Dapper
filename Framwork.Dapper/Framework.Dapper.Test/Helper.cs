using Framework.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Framework.Test
{
    internal static class Helper
    {
        /// <summary>只抓取command資訊，不會真的執行</summary>
        public static List<DbTraceContext.CommandInfo> GetCommandInfos(string sqlStr, object model)
        {
            using (var trace = new DbTraceContext(false))
            {
                using (var conn = OpenConnection())
                {
                    var data = conn.Query(sqlStr, model).FirstOrDefault();
                }
                return trace.History;
            }
        }

        public static DbConnectionWrapper OpenConnection()
        {
            return DbHelper.OpenConnection("test.local");
        }

        private static Dictionary<Type, string> fieldTypeMapping = new Dictionary<Type, string>
        {
            [typeof(byte)] = "tinyint",
            //[typeof(sbyte)] = DbType.SByte,
            [typeof(short)] = "smallint",
            //[typeof(ushort)] = DbType.UInt16,
            [typeof(int)] = "int",
            //[typeof(uint)] = DbType.UInt32,
            [typeof(long)] = "bigint",
            //[typeof(ulong)] = DbType.UInt64,
            [typeof(float)] = "real",
            [typeof(double)] = "float",
            [typeof(decimal)] = "decimal",
            [typeof(bool)] = "bit",
            [typeof(string)] = "nvarchar(100)",
            [typeof(char)] = "nchar(1)",
            [typeof(Guid)] = "uniqueidentifier",
            [typeof(DateTime)] = "datetime",
            [typeof(DateTimeOffset)] = "datetimeoffset",
            [typeof(TimeSpan)] = "time",
            [typeof(byte[])] = "binary",
            [typeof(object)] = "sql_variant"
        };

        private static Dictionary<string, string> GetColumnTypeSql(Type modelType) 
        {
            return ModelTableInfo.Get(modelType).Columns.ToDictionary(n => n.ColumnName, n =>
            {
                var fieldType = n.EnumInfo?.ValueType ?? Nullable.GetUnderlyingType(n.ElementType) ?? n.ElementType;
                return fieldTypeMapping[fieldType];
            });
        }

        public static string CreateTempTable<T>(this IDbConnection conn) => conn.CreateTempTable(typeof(T));
        public static string CreateTempTable(this IDbConnection conn, Type modelType)
        {
            var tableName = $"#Tmp_{modelType.Name}_{Guid.NewGuid().ToString("N").Substring(0, 10)}";
            var fields = string.Join(",", GetColumnTypeSql(modelType).Select(n => $"{n.Key} {n.Value}"));
            var sql = $@"create table {tableName} ({fields})";
            conn.Execute(sql);
            return tableName;
        }

        internal static void AssertSqlDatetimeEqual(DateTime expected, DateTime actual)
        {
            //從資料庫抓出來的Datetime.Kind都是Unspecified, 所以用u的字串格式來比較
            Assert.Equal(expected.ToString("u"), actual.ToString("u"));
        }

        internal static List<DbTraceContext.ParameterInfo> Verify(this List<DbTraceContext.ParameterInfo> parameters, string name, object value, DbType? dbType = null, int? size = null)
        {
            var param = parameters.FirstOrDefault(p => p.Name == name);
            if (param == null) throw new Exception($"不存在參數{name}");
            Assert.Equal(value, param.Value);
            if (dbType.HasValue) Assert.Equal(dbType.Value, param.DbType);
            if (size.HasValue) Assert.Equal(size.Value, param.Size);
            return parameters;
        }

        internal static DbTraceContext.CommandInfo Verify(this DbTraceContext.CommandInfo commandInfo, string commandText)
        {
            Assert.Equal(commandInfo.CommandText, commandText);
            return commandInfo;
        }
    }
}
