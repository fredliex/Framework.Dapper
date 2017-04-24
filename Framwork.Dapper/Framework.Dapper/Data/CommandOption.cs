using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    public struct CommandOption
    {
        /// <summary>command 執行timeout</summary>
        public int? CommandTimeout;

        private CommandType commandType;
        /// <summary>Command類型</summary>
        public CommandType CommandType
        {
            get => commandType == default(CommandType) ? (commandType = CommandType.Text) : commandType;
            set => commandType = value;
        }

        private bool? buffered;
        /// <summary>select的結果是否緩衝。建議非大量資料時皆採緩衝以加快處理速度並讓連線盡早釋放。</summary>
        public bool Buffered
        {
            get => buffered.GetValueOrDefault(true);
            set => buffered = value;
        }

        internal CommandDefinition ToDefinition(string commandText, object parameters, IDbTransaction transaction) =>
            new CommandDefinition(commandText, parameters, transaction, CommandTimeout, CommandType, Buffered ? CommandFlags.Buffered : CommandFlags.None);
    }
}
