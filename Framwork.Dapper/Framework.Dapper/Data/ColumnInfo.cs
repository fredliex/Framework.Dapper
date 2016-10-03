using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal abstract class ColumnInfo
    {
        /// <summary>成員</summary>
        internal abstract MemberInfo Member { get; }
        /// <summary>成員名稱</summary>
        internal string MemberName { get; private set; }
        /// <summary>成員類型</summary>
        internal abstract MemberTypes MemberType { get; }
        /// <summary>對應的欄位名稱</summary>
        internal string Name { get; private set; }
        /// <summary>成員型態</summary>
        internal Type ValueType { get; }
        /// <summary>是否為鍵值</summary>
        internal bool IsKey { get; private set; } = false;
        /// <summary>當設定為true時, 於update與delete時是否會檢查此欄位必須與資料庫一致</summary>
        internal bool IsConcurrencyCheck { get; private set; } = false;
        /// <summary>查詢時針對字串欄位是否去掉右邊空白。此設定僅作用於查詢時。</summary>
        internal bool IsTrimRight { get; private set; } = false;
        /// <summary>model類型為null時所對應的資料庫欄位值。若為沒對應則此屬性為null。此為雙向對應。</summary>
        internal object NullMapping { get; private set; } = null;

        internal abstract void EmitGenerateGet(ILGenerator il);

        private ColumnInfo(MemberInfo member, Type valueType, ColumnAttribute columnAttribute)
        {
            MemberName = member.Name;
            ValueType = valueType;
            //如果沒定義ColumnAttribute 或是 ColumnAttribute.Name 是null或是空白代表於類型名稱同名
            if (columnAttribute == null)
            {
                Name = MemberName;
            }
            else
            {
                //未設定欄位名稱或是空白的話, 以屬性名稱視為欄位名稱
                Name = string.IsNullOrWhiteSpace(columnAttribute.Name) ? MemberName : columnAttribute.Name;
                var behavior = columnAttribute.Behavior;
                IsConcurrencyCheck = (behavior & ColumnBehavior.ConcurrencyCheck) != 0;
                IsKey = (behavior & ColumnBehavior.Key) != 0;
                IsTrimRight = (behavior & ColumnBehavior.TrimRight) != 0;
                //如果model屬性類型為可null的，則設定model屬性為null時資料庫對應的特定值
                if (!ValueType.IsValueType || Nullable.GetUnderlyingType(ValueType) != null) NullMapping = columnAttribute.NullMapping;
                //如果是同步檢核的欄位的話, 則判斷model屬性類型必須為DateTime或是Nullable<DateTime>
                if (IsConcurrencyCheck && ValueType != typeof(DateTime) && ValueType != typeof(DateTime?))
                    throw new InvalidOperationException("ColumnBehavior.ConcurrencyCheck只允許設定於類型為DateTime或Nullable<DateTime>的成員上。");
            }
        }

        private sealed class FieldColumnInfo : ColumnInfo
        {
            private FieldInfo member;
            internal override MemberInfo Member { get { return member; } }
            internal override MemberTypes MemberType { get { return MemberTypes.Field; } }

            internal FieldColumnInfo(FieldInfo field, ColumnAttribute columnAttribute) : base(field, field.FieldType, columnAttribute)
            {
                member = field;
            }

            internal override void EmitGenerateGet(ILGenerator il)
            {
                il.Emit(OpCodes.Ldfld, member);
            }
        }

        private sealed class PropertyColumnInfo : ColumnInfo
        {
            private PropertyInfo member;
            private OpCode callOpCode;
            private MethodInfo getMethod;
            internal override MemberInfo Member { get { return member; } }
            internal override MemberTypes MemberType { get { return MemberTypes.Property; } }

            internal PropertyColumnInfo(PropertyInfo property, ColumnAttribute columnAttribute, bool isStructModel) : base(property, property.PropertyType, columnAttribute)
            {
                member = property;
                callOpCode = isStructModel ? OpCodes.Call : OpCodes.Callvirt;
                getMethod = property.GetGetMethod(true) ?? property.GetGetMethod(false);
            }

            internal override void EmitGenerateGet(ILGenerator il)
            {
                il.EmitCall(callOpCode, getMethod, null);
            }
        }


        /*
         * 有繼承IModel時，才會考慮TableAttribute, ColumnAttribute, NonColumnAttribute，且會逐繼承鏈來處理。
         * 否則只採用目前type的public成員，而不考慮繼承的議題。
         * 比照Dapper的規則: 如果類似tuple的, 則依照建構式的順序, 否則就依照名稱排序
         */
        internal static List<ColumnInfo> Resolve(Type modelType, bool hasModelInterface, bool isStruct)
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
                columns = members.OrderBy(n => n.Key)
                    .Select(n => n.Value.isField ? 
                        (ColumnInfo)new FieldColumnInfo((FieldInfo)n.Value.member, n.Value.attr) : 
                        new PropertyColumnInfo((PropertyInfo)n.Value.member, n.Value.attr, isStruct)
                    );
            }
            else
            {
                //沒實作IModel時, 只抓public屬性, 不需逐繼承鍊處理
                columns = modelType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.GetIndexParameters().Length == 0).Select(n => new PropertyColumnInfo(n, n.GetAttribute<ColumnAttribute>(false), isStruct))
                    .OrderBy(n => n.MemberName);
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
    }
}
