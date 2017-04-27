using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>
    /// <para>設定列舉在資料庫中的對應值。僅適用於Enum的成員。</para>
    /// <para>通常只有Enum須對應string或是char的時候才會用到<see cref="DbValueAttribute"/>。</para>
    /// <para>一旦Enum有任何一個成員有設定<see cref="DbValueAttribute"/>時, 會檢查設定的值必須為值類型且類型必須都一致。另外存入資料庫時, 將檢查存入值必須是有定義<see cref="DbValueAttribute"/>的。</para>
    /// <para>model存入資料庫的時候，會先處理 <see cref="DbValueAttribute.Value"/> 的null值 再處理 <see cref="ColumnAttribute.NullDbValue"/>。</para>
    /// <para>資料庫轉成model的時候，會先處理 <see cref="ColumnAttribute.NullDbValue"/> 再處理 <see cref="DbValueAttribute.Value"/>的null值。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class DbValueAttribute : Attribute
    {
        /// <summary>於資料庫中的對應值，必須是值類型。</summary>
        /// <remarks>即使型別是object, 但attribute支援的類型僅有bool、byte、char、double、float、int、long、short、string, object, Enum, typeof(), null</remarks>
        public object Value;

        /// <summary></summary>
        /// <param name="value">於資料庫中的對應值，必須是值類型。</param>
        public DbValueAttribute(object value)
        {
            Value = value;
        }


        /// <summary>取得Enum與DbValue的對應</summary>
        /// <param name="enumType">列舉型別</param>
        /// <param name="nullEnum">DbValue為null時候所對應的Enum，null表示沒有。</param>
        /// <param name="dbValueUnderlyingType">DbValue的型態，不會是nullable。</param>
        /// <returns>回傳enum 與 dbValue的對應關係。key是enum，value是dbValue。如果該Enum沒有定義任何DbValueAttribute，則回傳null。</returns>
        internal static List<KeyValuePair<object, object>> GetMapping(Type enumType, out object nullEnum, out Type dbValueUnderlyingType)
        {
            var fields = enumType.GetFields(BindingFlags.Static | BindingFlags.Public);
            var list = new List<KeyValuePair<object, object>>(fields.Length);   //key是enum, value是dbValue

            dbValueUnderlyingType = null;
            nullEnum = null;

            #region 取得成員值以及對應值
            foreach (FieldInfo field in fields)
            {
                var attr = field.GetAttribute<DbValueAttribute>(false);
                if (attr == null) continue;
                var dbValue = attr.Value;
                var enumValue = field.GetValue(null);
                if (dbValue == null)
                {
                    if (nullEnum != null) throw new Exception($"不可同時多個{nameof(DbValueAttribute)}都定義為null。");
                    nullEnum = enumValue;
                }
                else
                {
                    var tmpDbType = dbValue.GetType();
                    if (tmpDbType == dbValueUnderlyingType)
                    {
                        //不需作任何特殊處理, 會有這判斷是因為預期Enum成員ValueAttribute的定義值都是同類型, 所以優先判斷相等
                    }
                    else if (dbValueUnderlyingType == null)
                    {
                        dbValueUnderlyingType = tmpDbType;
                    }
                    else
                    {
                        throw new Exception($"當Enum定義{nameof(DbValueAttribute)}時，{nameof(DbValueAttribute)}所設定的Value型別必須一致。");
                    }
                    list.Add(new KeyValuePair<object, object>(enumValue, dbValue));
                }
            }
            #endregion

            //沒有dbValueUnderlyingType表示並無定義DbValueAttribute, 就回傳null
            if (dbValueUnderlyingType == null) return null;
            if (enumType.IsDefined(typeof(FlagsAttribute))) throw new NotSupportedException($"目前不支援有標示{nameof(FlagsAttribute)}的列舉");
            return list;
        }
    }
}
