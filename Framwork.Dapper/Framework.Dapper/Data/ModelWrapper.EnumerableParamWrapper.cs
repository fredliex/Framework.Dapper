using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class ModelWrapper
    {
        /// <summary>
        /// 針對集合用的
        /// </summary>
        internal sealed class EnumerableParamWrapper: IEnumerable
        {
            #region GetEnumerator
            [Serializable]
            private struct Enumerator : IEnumerator
            {
                private IEnumerator enumerator;
                private ParamWrapper wrapper;

                internal Enumerator(IEnumerable list, Action<IDbCommand, object> paramInfoGenerator)
                {
                    wrapper = new ParamWrapper { ParamGenerator = paramInfoGenerator };
                    enumerator = list.GetEnumerator();
                }
                public object Current
                {
                    get
                    {
                        wrapper.Model = enumerator.Current;
                        return wrapper;
                    }
                }

                public bool MoveNext()
                {
                    return enumerator.MoveNext();
                }

                public void Reset()
                {
                    enumerator.Reset();
                }
            }
            public IEnumerator GetEnumerator()
            {
                return new Enumerator(models, paramGenerator);
            }
            #endregion

            private readonly IEnumerable models;
            private readonly Action<IDbCommand, object> paramGenerator;

            internal EnumerableParamWrapper(IEnumerable models, Action<IDbCommand, object> paramGenerator)
            {
                this.models = models;
                this.paramGenerator = paramGenerator;
            }

        }
    }
}
