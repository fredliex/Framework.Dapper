using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Framework.Data
{
    public sealed partial class ModelMerger<T> where T : IDbModel
    {
        private static Comparer defaultComparer = null;
        private Comparer comparer;

        public ReadOnlyCollection<T> Same { get; private set; }

        public ReadOnlyCollection<T> Insert { get; private set; }

        public ReadOnlyCollection<T> Update { get; private set; }

        public ReadOnlyCollection<T> Delete { get; private set; }

        public ModelMerger(IEnumerable<T> oldModels, IEnumerable<T> newModels, Expression<Func<T, object>>[] keyExpressions)
        {
            comparer = InitComparer(keyExpressions);

            //先比對key判斷insert, delete 與 keySame
            var insert = new Set(newModels, comparer);
            var delete = new List<T>();
            var keySame = new List<KeyValuePair<T, T>>();       //放鍵值相同的, 後續還須判斷為Same還是Update. KeyValuePair<oldItem, newItem>
            foreach (var oldItem in oldModels)
            {
                if (insert.Remove(oldItem, out var newItem))
                    keySame.Add(new KeyValuePair<T, T>(oldItem, newItem));
                else
                    delete.Add(oldItem);
            }

            //再將keySame區分為Same還是Update
            var same = new List<T>();
            var update = new List<T>();
            foreach (var n in keySame)
            {
                (comparer.ValueEquals(n.Key, n.Value) ? same : update).Add(n.Value);
            }

            Same = same.AsReadOnly();
            Update = update.AsReadOnly();
            Delete = delete.AsReadOnly();
            Insert = insert.ToList().AsReadOnly();
        }

        private Comparer InitComparer(Expression<Func<T, object>>[] keyExpressions)
        {
            var columns = ModelTableInfo.Get(typeof(T)).Columns;
            var members = columns.Select(col => col.Member);
            var keyMembers = keyExpressions?.Select(exp =>
            {
                var member = GetMemberInfo(exp);
                if (!columns.Any(col => col.Member == member)) throw new Exception($"{member} 不是 {typeof(T)} 的成員。");
                return member;
            });
            if (keyMembers != null && keyMembers.Any()) return new Comparer(members, keyMembers);

            if (defaultComparer != null) return defaultComparer;
            keyMembers = columns.Where(n => n.IsKey).Select(n => n.Member);
            return defaultComparer = new Comparer(members, keyMembers);
        }

        private static MemberInfo GetMemberInfo(Expression<Func<T, object>> expression) =>
            expression == null ? null : GetMemberInfo(expression.Body);

        private static MemberInfo GetMemberInfo(Expression expression) => 
            expression is MemberExpression memberExp ? memberExp.Member :
            expression is UnaryExpression unaryExp ? GetMemberInfo(unaryExp.Operand) :
            null;
    }
}
