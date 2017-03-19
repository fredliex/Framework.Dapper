using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Framework.Data.ModelWrapper;

namespace Framework.Data
{
    /// <summary>欄位資訊</summary>
    /// <remarks>僅針對DataModel使用，Dictionary不會用到。</remarks>
    internal class ColumnInfo
    {
        /// <summary>Model成員名稱</summary>
        public string MemberName { get; private set; }

        /// <summary>若非集合的話則為本身型別。若為集合的話則為元素型別。可能會是Nullable類型。</summary>
        public Type ElementType { get; private set; }

        /// <summary>若非集合的話則為本身型別。若為集合的話則為元素型別。為非Nullable類型。</summary>
        public Type ElementUnderlyingType { get; private set; }

        /// <summary>是否為多重值，也就是資料庫的in語法</summary>
        /// <remarks>僅作為參數時才有用</remarks>
        public bool IsMultiValue { get; private set; }

        /// <summary>對應的資料庫欄位名稱</summary>
        public string ColumnName { get; private set; }

        /// <summary>是否為資料庫鍵值</summary>
        public bool IsKey { get; private set; }

        /// <summary>當設定為true時, 於update與delete時是否會檢查此欄位必須與資料庫一致</summary>
        public bool IsConcurrencyCheck { get; private set; }

        /// <summary>查詢時針對字串欄位是否去掉右邊空白。此設定僅作用於查詢時。</summary>
        public bool IsTrimRight { get; private set; }

        /// <summary>model類型為null時所對應的資料庫欄位值。若為沒對應則此屬性為null。此為雙向對應。</summary>
        public object NullMapping { get; private set; }

        /// <summary>列舉資訊。如果非列舉的話，會是null。</summary>
        public EnumInfo EnumInfo { get; private set; }

        protected ColumnInfo(string memberName, Type memberType, ColumnAttribute columnAttribute)
        {
            MemberName = memberName;
            ColumnName = memberName;

            var elemType = InternalHelper.GetElementType(memberType); //抓取集合元素型別, 回傳null表示非多重值
            IsMultiValue = elemType != null;
            ElementUnderlyingType = ElementType = elemType ?? memberType;
            var isStructType = ElementType.IsValueType; //型別是否為值類型
            Type nullableType = null;
            if (isStructType)
            {
                nullableType = Nullable.GetUnderlyingType(ElementType);
                if (nullableType != null) ElementUnderlyingType = nullableType;
                EnumInfo = EnumInfo.Get(nullableType ?? ElementType);
            }
            if (columnAttribute != null)
            {
                //未設定欄位名稱或是空白的話, 以屬性名稱視為欄位名稱
                if (!string.IsNullOrWhiteSpace(columnAttribute.Name)) ColumnName = columnAttribute.Name;

                var behavior = columnAttribute.Behavior;
                //DateTime 或是 DateTimeOffset 才能給ConcurrencyCheck
                IsConcurrencyCheck = (behavior & ColumnBehavior.ConcurrencyCheck) != 0;
                if (IsConcurrencyCheck && ElementUnderlyingType != typeof(DateTime) && ElementUnderlyingType != typeof(DateTimeOffset))
                    throw new Exception($"{typeof(ColumnBehavior).Name}.{nameof(ColumnBehavior.ConcurrencyCheck)}只能定義於DateTime或DateTimeOffset型別的屬性上");
                IsKey = (behavior & ColumnBehavior.Key) != 0;
                IsTrimRight = (behavior & ColumnBehavior.TrimRight) != 0;
                //如果model屬性類型為可null的，則設定model屬性為null時資料庫對應的特定值
                if (!isStructType || nullableType != null) NullMapping = columnAttribute.NullMapping;
            }
        }

        #region 
        /// <summary>解析Dictionary取得型別資訊</summary>
        /// <param name="dicts">複數的dictionary</param>
        /// <returns></returns>
        internal static IEnumerable<ColumnInfo> Resolve(IEnumerable<IDictionary<string, object>> dicts)
        {
            //不考慮key不一致的情況, 單純只用第一個node來抓key
            //若value若為null的話, 抓後面第一個非null的value來推論型別. 如果都是null, 則型別就當做是object
            var lackNames = new List<string>(); //放置無法推斷value型別的Key name
            var colTypes = dicts.FirstOrDefault().ToDictionary(n => n.Key, n =>
            {
                var type = n.Value?.GetType();
                if (type == null) lackNames.Add(n.Key);
                return type;
            });
            //巡迴dict來推斷value型別, 無法推斷的話就當是object型別
            object value;
            lackNames.ForEach(key => colTypes[key] = dicts.Select(n => n.TryGetValue(key, out value) ? value?.GetType() : null).FirstOrDefault(n => n != null) ?? typeof(object));
            //回傳
            return colTypes.Select(n => new ColumnInfo(n.Key, n.Value, null));
        }
        #endregion
    }
}
