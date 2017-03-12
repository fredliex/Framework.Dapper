using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class DbWrapperHelper
    {
        interface IWrappedDb
        {
            /*
            /// <summary>真正的實體</summary>
            object WrapInstance { get; }
            */
        }

        interface IWrappedDb<T> : IWrappedDb
        {
            /// <summary>真正的實體</summary>
            T Instance { get; }
        }
    }
}
