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
        public static T Verify<T>(this T parameters, string name, object value, DbType? dbType = null, int? size = null) where T : IDataParameterCollection
        {
            if (!parameters.Contains(name)) throw new Exception($"不存在參數{name}");
            var param = (IDbDataParameter)parameters[name];
            Assert.Equal(value, param.Value);
            if (dbType.HasValue) Assert.Equal(dbType.Value, param.DbType);
            if (size.HasValue) Assert.Equal(size.Value, param.Size);
            return parameters;
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
    }
}
