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
    }
}
