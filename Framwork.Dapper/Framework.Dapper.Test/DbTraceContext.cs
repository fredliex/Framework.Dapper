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

            private static DbType[] stringTypes = new[] { DbType.String, DbType.StringFixedLength, DbType.AnsiString, DbType.AnsiStringFixedLength };
            public override string ToString() => 
                string.Format("{0}={1}", Name, Value == DBNull.Value ? "null" : stringTypes.Contains(DbType) ? $"'{((string)Value).Replace("'", "''")}'" : Value);
        }

        [ThreadStatic]
        internal static DbTraceContext Current = null;


        public readonly List<CommandInfo> History = new List<CommandInfo>();

        /// <summary>是否實際執行真正的DbCommand</summary>
        private bool executeRealCommand;
        public DbTraceContext(bool executeRealCommand = true)
        {
            this.executeRealCommand = executeRealCommand;
            Current = this;
        }
        public void Dispose()
        {
            Current = null;
        }
        public bool Add(IDbCommand command)
        {
            History.Add(new CommandInfo(command));
            return executeRealCommand;
        }
    }
}
