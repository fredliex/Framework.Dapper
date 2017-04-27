using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>DbCommand攔截器</summary>
    public interface IDbCommandIntercept
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command"></param>
        /// <param name="executeDelegate"></param>
        /// <returns></returns>
        T CommandExecute<T>(DbCommand command, Func<DbCommand, T> executeDelegate);
    }
}
