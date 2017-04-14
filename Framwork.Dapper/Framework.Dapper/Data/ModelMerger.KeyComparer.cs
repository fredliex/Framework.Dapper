using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial struct ModelMerger<T>
    {
        private sealed class KeyComparer : IEqualityComparer<T>
        {
            private Func<T, int> funcGetHashCode;
            private Func<T, T, bool> funcEquals;

            public KeyComparer(Func<T, int> funcGetHashCode, Func<T, T, bool> funcEquals)
            {
                this.funcGetHashCode = funcGetHashCode;
                this.funcEquals = funcEquals;
            }

            public bool Equals(T x, T y) => funcEquals(x, y);

            public int GetHashCode(T obj) => funcGetHashCode(obj);
        }
    }
}
