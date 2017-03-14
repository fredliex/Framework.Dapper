using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Framework.Test
{
    internal sealed class DbTraceContext : IDisposable
    {
        internal sealed class CommandInfo
        {
            public string CommandText;
            public List<ParameterInfo> Parameters;
            public CommandInfo(IDbCommand command)
            {
                CommandText = command.CommandText;
                Parameters = command.Parameters.Cast<IDbDataParameter>().Select(p => new ParameterInfo(p)).ToList();
            }

            public CommandInfo Verify(string name, object value, DbType? dbType = null, int? size = null) 
            {
                var param = Parameters.FirstOrDefault(p => p.Name == name);
                if (param == null) throw new Exception($"不存在參數{name}");
                Assert.Equal(value, param.Value);
                if (dbType.HasValue) Assert.Equal(dbType.Value, param.DbType);
                if (size.HasValue) Assert.Equal(size.Value, param.Size);
                return this;
            }
        }
        internal sealed class ParameterInfo
        {
            public string Name;
            public object Value;
            public DbType DbType;
            public int Size;
            public ParameterInfo(IDbDataParameter parameter)
            {
                Name = parameter.ParameterName;
                Value = parameter.Value;
                DbType = parameter.DbType;
                Size = parameter.Size;
            }
        }

        [ThreadStatic]
        internal static DbTraceContext Current = null;

        public readonly List<CommandInfo> History = new List<CommandInfo>();
        public DbTraceContext()
        {
            Current = this;
        }
        public void Dispose()
        {
            Current = null;
        }
        public void Add(IDbCommand command)
        {
            History.Add(new CommandInfo(command));
        }
    }
}
