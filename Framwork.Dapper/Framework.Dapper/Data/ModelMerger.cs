using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Framework.Data
{
    public partial struct ModelMerger<T>
    {
        private static Func<T, int> funcKeyGetHashCode;
        private static Func<T, T, bool> funcKeyEquals;
        private static IEqualityComparer<T> keyComparer;
        private static Func<T, T, bool> funcValueEquals;

        static ModelMerger()
        {

        }

        public ReadOnlyCollection<T> Same { get; private set; }

        public ReadOnlyCollection<T> Insert { get; private set; }

        public ReadOnlyCollection<T> Update { get; private set; }

        public ReadOnlyCollection<T> Delete { get; private set; }

        public ModelMerger(IEnumerable<T> oldModels, IEnumerable<T> newModels)
        {
            Same = Insert = Update = Delete = null;

            //先比對key判斷insert, delete 與 keySame
            var insert = new Set(newModels, keyComparer);
            var delete = new List<T>();
            var keySame = new List<(T oldItem, T newItem)>();   //key值相同的, 後續還須判斷為Same還是Update
            foreach (var oldItem in oldModels)
            {
                if (insert.Remove(oldItem, out var newItem))
                    keySame.Add((oldItem, newItem));
                else
                    delete.Add(oldItem);
            }

            //再將keySame區分為Same還是Update
            var same = new List<T>();
            var update = new List<T>();
            foreach (var n in keySame)
            {
                (funcValueEquals(n.oldItem, n.newItem) ? same : update).Add(n.newItem);
            }

            Same = same.AsReadOnly();
            Update = update.AsReadOnly();
            Delete = delete.AsReadOnly();
            Insert = insert.ToList().AsReadOnly();
        }
    }
}
