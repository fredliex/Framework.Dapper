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
        /// <summary>
        /// The effective timeout for the command
        /// </summary>
        public int? CommandTimeout;

        private CommandType commandType;
        /// <summary>
        /// The type of command that the command-text represents
        /// </summary>
        public CommandType CommandType
        {
            get => commandType == default(CommandType) ? (commandType = CommandType.Text) : commandType;
            set => commandType = value;
        }

        private bool? buffered;
        /// <summary>
        /// Should data be buffered before returning?
        /// </summary>
        public bool Buffered
        {
            get => buffered.GetValueOrDefault(true);
            set => buffered = value;
        }

        internal CommandDefinition ToDefinition(string commandText, object parameters, IDbTransaction transaction) =>
            new CommandDefinition(commandText, parameters, transaction, CommandTimeout, CommandType, Buffered ? CommandFlags.Buffered : CommandFlags.None);
    }
}
