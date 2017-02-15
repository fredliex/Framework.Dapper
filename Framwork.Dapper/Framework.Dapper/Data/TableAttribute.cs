using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /*
     * System.ComponentModel.DataAnnotations.Schema下也有個TableAttribute, 但因為提供的屬性不同, 所以還是自定義一個TableAttribute
     * 因為應該不需要using System.ComponentModel.DataAnnotations.Schema, 所以暫不顧慮TableAttribute同時定義於TMNewa 與 System.ComponentModel.DataAnnotations.Schema的錯誤
     * 如果需考慮的話, TableAttribute就移到TMNewa的namespace下, 不過目前先不用
     * 
     * Inherited = false 是因為即使是衍生的model不太可能會對到同一個table
     */

    /// <summary>變更對應Table。一旦類型有繼承IDataModel則預設為TableAttribute(Name=類型名稱)</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class TableAttribute : Attribute
    {
        /// <summary>對應Table名稱, 若為null或為string.Empty表示與類型名稱一樣。</summary>
        public string Name = null;

        /// <summary>Table的資料庫名稱, 若為null或是string.Empty表示為預設所在資料庫。</summary>
        public string Database = null;

        /// <summary>Table的Schema名稱, 若為null或是string.Empty表示為預設的Schema。</summary>
        public string Schema = null;

        /// <summary></summary>
        /// <param name="name">對應Table名稱, 若為null或為string.Empty示意為同類型名稱</param>
        public TableAttribute(string name)
        {
            Name = name;
        }
    }
}
