using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>
    /// 設定列舉在資料庫中的對應值。僅適用於Enum的成員。
    /// 一旦Enum有任何一個成員有設定ValueAttribute時, 會檢查設定的值必須為值類型且類型必須都一致。另外存入資料庫時, 將檢查存入值必須是有定義ValueAttribute的。
    /// 對應值可設定為null, 但ValueAttribute的null處理會在ColumnAttribute的NullValue之後
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class ValueAttribute : Attribute
    {
        /// <summary>於資料庫中的對應值，必須是值類型。</summary>
        /// <remarks>即使型別是object, 但attribute支援的類型僅有bool、byte、char、double、float、int、long、short、string, object, Enum, typeof(), null</remarks>
        public object Value;

        /// <summary></summary>
        /// <param name="value">於資料庫中的對應值，必須是值類型。</param>
        public ValueAttribute(object value)
        {
            Value = value;
        }
    }
}
