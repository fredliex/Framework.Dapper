using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class ColumnInfoCollection : IReadOnlyCollection<ColumnInfo>
    {
        private ColumnInfo[] cols;

        //由member name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> memberMap;
        //由column name所建立的dictionary, value為欄位序, lazy產生
        private Dictionary<string, int> columnMap;
        //設定為IsConcurrencyCheck的欄位序
        private int? concurrencyCheckColumnIndex = null;

        public int Count
        {
            get { return cols.Length; }
        }

        internal ColumnInfoCollection(Type modelType, bool? hasModelInterface, bool? isStructModel) 
        {
            var columns = Resolve(modelType, hasModelInterface ?? typeof(IModel).IsAssignableFrom(modelType), isStructModel ?? modelType.IsValueType);
            Init(columns);
        }

        internal ColumnInfoCollection(IEnumerable<KeyValuePair<string, object>> dictionary)
        {
            var columns = Resolve(dictionary);
            Init(columns);
        }

        internal ColumnInfoCollection(IEnumerable<ColumnInfo> columns)
        {
            Init(columns);
        }

        private void Init(IEnumerable<ColumnInfo> columns)
        {
            cols = columns.ToArray();
            memberMap = new Dictionary<string, int>(cols.Length);
            columnMap = new Dictionary<string, int>(cols.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cols.Length; i++)
            {
                var column = cols[i];
                memberMap[column.MemberName] = columnMap[column.ColumnName] = i;
                if (column.IsConcurrencyCheck)
                {
                    if (concurrencyCheckColumnIndex.HasValue) throw new InvalidOperationException("最多只能一個欄位設定ColumnAttribute.IsConcurrencyCheck為True。");
                    concurrencyCheckColumnIndex = i;
                }
            }
        }


        public ColumnInfo GetColumn(string columnName)
        {
            int colIndex;
            return columnMap.TryGetValue(columnName, out colIndex) ? cols[colIndex] : null;
        }

        public IEnumerator<ColumnInfo> GetEnumerator()
        {
            return ((IEnumerable<ColumnInfo>)cols).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return cols.GetEnumerator();
        }


        #region 解析Dictionary<string, object>
        private static IEnumerable<ColumnInfo> Resolve(IEnumerable<KeyValuePair<string, object>> dictionary)
        {
            return dictionary.Select(n =>
            {
                var valueType = n.Value?.GetType();
                return new ColumnInfo
                {
                    MemberName = n.Key,
                    ValueType = valueType,
                    IsEnumerableValue = ColumnInfo.IsEnumerableType(valueType),
                    ColumnName = n.Key
                };
            });
        }
        #endregion

        #region 解析model
        /*
         * 有繼承IModel時，才會考慮TableAttribute, ColumnAttribute, NonColumnAttribute，且會逐繼承鏈來處理。
         * 否則只採用目前type的public成員，而不考慮繼承的議題。
         * 比照Dapper的規則: 如果類似tuple的, 則依照建構式的順序, 否則就依照名稱排序
         */
        private static List<ColumnInfo> Resolve(Type modelType, bool hasModelInterface, bool isStructModel)
        {
            IEnumerable<ColumnInfo> columns;
            if (hasModelInterface)
            {
                //這邊只是為了建立一個空的匿名物件字典, key是member name
                var members = Enumerable.Empty<int>().ToDictionary(x => (string)null, x => new { member = (MemberInfo)null, isPublic = true, isField = true, attr = (ColumnAttribute)null });
                Action<Type, bool, BindingFlags> fillMembers = (type, isField, bindingFlags) =>
                {
                    var isPublic = (bindingFlags & BindingFlags.Public) == BindingFlags.Public;
                    //屬性的話排除有IndexParameters的
                    foreach (var member in isField ? (IEnumerable<MemberInfo>)type.GetFields(bindingFlags) : type.GetProperties(bindingFlags).Where(p => p.GetIndexParameters().Length == 0))
                    {
                        //如果public field有NonColumnAttribute就不處理
                        if (isPublic && member.IsDefined(typeof(NonColumnAttribute), false)) continue;
                        var attr = member.GetAttribute<ColumnAttribute>(false);
                        //如果nonpublic field且沒NonColumnAttribute就不處理
                        if (!isPublic && attr == null) continue;
                        members[member.Name] = new { member, isPublic, isField, attr };
                    }
                };
                //有實作IModel時為了處理member Attribute的Inherited問題, 所以要順著繼承鍊來處理
                var inheritLink = new List<Type>();
                for (var type = modelType; type != typeof(object); type = type.BaseType) inheritLink.Add(type);
                for (var inheritIndex = inheritLink.Count - 1; inheritIndex >= 0; inheritIndex--)
                {
                    var type = inheritLink[inheritIndex];
                    fillMembers(type, true, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
                    fillMembers(type, true, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic);
                    fillMembers(type, false, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
                    fillMembers(type, false, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.NonPublic);
                }
                columns = members.OrderBy(n => n.Key).Select(n => new ColumnInfo(n.Value.member, null, n.Value.attr, isStructModel));
            }
            else
            {
                //沒實作IModel時, 只抓public屬性, 不需逐繼承鍊處理
                columns = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.GetIndexParameters().Length == 0)
                    .Select(p => new ColumnInfo(p, null, p.GetAttribute<ColumnAttribute>(false), isStructModel))
                    .OrderBy(n => n.Member.Name);
            }
            return SortColumn(modelType, columns);
        }

        //判斷是不是像tuple, 是的話就依照建構式排序, 否則依照原本名稱排序的方式
        private static List<ColumnInfo> SortColumn(Type modelType, IEnumerable<ColumnInfo> columns)
        {
            var listByName = columns.ToList();
            //如果建構式不只一個, 或是建構式的參數數量和欄位數不符, 就不改變排序
            var ctors = modelType.GetConstructors();
            if (ctors.Length != 1) return listByName;
            var ctorParams = ctors[0].GetParameters();
            if (listByName.Count != ctorParams.Length) return listByName;
            //如果建構式只有一個, 且參數與欄位名稱相符, 就改用參數的順序回傳, 否則就傳回原順序
            var dict = listByName.ToDictionary(n => n.MemberName, StringComparer.OrdinalIgnoreCase);
            var listByCtor = new List<ColumnInfo>(ctorParams.Length);
            ColumnInfo tmpInfo;
            for (var i = 0; i < ctorParams.Length; i++)
            {
                if (!dict.TryGetValue(ctorParams[i].Name, out tmpInfo)) return listByName;
                listByCtor.Add(tmpInfo);
            }
            return listByCtor;
        }
        #endregion
    }
}
