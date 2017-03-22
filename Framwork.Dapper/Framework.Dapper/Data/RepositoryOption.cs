using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>Repository參數</summary>
    public struct RepositoryOption
    {
        /// <summary>強制指定Database名稱</summary>
        public string Database;

        /// <summary>強制指定Schema名稱</summary>
        public string Schema;

        /// <summary>強制指定Table名稱</summary>
        public string Table;
    }
}
