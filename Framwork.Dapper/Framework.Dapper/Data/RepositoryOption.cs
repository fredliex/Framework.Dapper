using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    public sealed class RepositoryOption
    {
        /// <summary>強制指定Database名稱</summary>
        public string Database = null;

        /// <summary>強制指定Schema名稱</summary>
        public string Schema = null;

        /// <summary>強制指定Table名稱</summary>
        public string Table = null;
    }
}
