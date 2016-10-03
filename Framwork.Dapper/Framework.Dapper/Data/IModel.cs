using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    /// <summary>
    /// 當Model繼承此介面時才會獲得Repository的支持以及在DbHelper中有對model成員cache的效果
    /// <para>若是回傳資料的話可支援Field以及NonPublic。但若要當作傳入參數的話尚不支援Field以及NonPublic，僅可用Public Property。</para>
    /// </summary>
    /// <remarks>
    /// <para>採用interface而不是attribute的原因是使用interface的話可於Extensions搭配約束條件使用，而attribute則不行。</para>
    /// <para>當類別實作IModel時，公開(public)的成員(Property與Field)會自動Mapping。若公開成員要取消對應或是非公開(nonpublic)的成員要加入對應時，可用<see cref="ColumnAttribute">ColumnAttribute</see>來控制。</para>
    /// </remarks>
    public interface IModel
    {

    }
}
