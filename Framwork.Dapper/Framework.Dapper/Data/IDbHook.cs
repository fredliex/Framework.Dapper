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
    public interface IDbHook
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command"></param>
        /// <param name="executeDelegate"></param>
        /// <returns></returns>
        T CommandExecute<T>(IDbCommand command, Func<IDbCommand, T> executeDelegate);
    }
}
