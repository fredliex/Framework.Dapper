using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>變更對應Column。一旦類型有繼承IModel則預設公開類型為ColumnAttribute(Name=類型名稱, IsPrimaryKey=false, NullValue=null)</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ColumnAttribute : Attribute
    {
        /// <summary>對應欄位名稱, 若為null或為string.Empty示意為同類型名稱</summary>
        public string Name = null;

        /// <summary>行為</summary>
        public ColumnBehavior Behavior;

        /// <summary>
        /// 類型為null時對應的資料庫欄位值。此設定僅在屬性類型為可null時才會生效。
        /// <para>注意：此為雙向對應。</para>
        /// <para>意謂若model屬性為null則資料庫欄位值會放NullMapping的值。</para>
        /// <para>相對的，若資料庫欄位為NullMapping的值，查詢出來的時候model的屬性值也會是null。。</para>
        /// </summary>
        public object NullMapping = null;

        /// <summary>欄位資訊</summary>
        /// <param name="name">對應欄位名稱, 若為null或為string.Empty示意為同類型名稱。</param>
        public ColumnAttribute(string name = null)
        {
            Name = name;
        }

        /// <summary>用來給SqlMapper裡面Il呼叫用的</summary>
        /// <param name="str"></param>
        /// <returns></returns>
        internal static string TrimRight(string str)
        {
            return str.TrimEnd(' ');
        }
    }
}
