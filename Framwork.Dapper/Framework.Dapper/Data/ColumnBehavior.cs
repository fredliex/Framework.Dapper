using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>欄位行為</summary>
    [Flags]
    public enum ColumnBehavior
    {
        /// <summary>鍵值。於update或是delete時預設條件欄位。</summary>
        Key = 1,

        /// <summary>
        /// 同步檢查欄位。
        /// <para>目前只能應用在DateTime或是DateTime?的成員上。</para>
        /// <para>insert時若成員類型為DateTime的話，會直接設定欄位值為getdate()。</para>
        /// <para>update時會直接設定此欄位值為getdate()。</para>
        /// <para>update與delete時會檢查此欄位是否有變動。</para>
        /// </summary>
        ConcurrencyCheck = 2,

        /// <summary>查詢時針對字串欄位是否去掉右邊空白。此設定僅作用於查詢時。</summary>
        /// <remarks>此方式是查出字串後呼叫TrimEnd，並非用sql來trim。所以會有效能的影響。</remarks>
        TrimRight = 4
    }
}
