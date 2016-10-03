using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>
    /// 
    /// </summary>
    public static class DbHelper
    {
        /// <summary>
        /// 
        /// </summary>
        public static IDbHook DbHook = null;

        internal static T InternalCommandExecuteWrap<T>(IDbCommand command, Func<IDbCommand, T> func)
        {
            var hook = DbHook;
            return hook == null ? func(command) : hook.CommandExecute(command, func);
        }
    }
}
