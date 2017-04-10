using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Framework.Data
{
    public struct ModelMerger<T> where T : IDataModel
    {
        private static Func<T, bool> keyComparer;
        private static Func<T, bool> valueComparer; 

        static ModelMerger()
        {

        }

        public ReadOnlyCollection<T> Same { get; private set; }

        public ReadOnlyCollection<T> Insert { get; private set; }

        public ReadOnlyCollection<T> Update { get; private set; }

        public ReadOnlyCollection<T> Delete { get; private set; }

        public ModelMerger(IEnumerable<T> oldModels, IEnumerable<T> newModels)
        {
            //Same = Key相同, Value相同
            //Insert = Key不同, oldModel沒有且newModel有
            //Delete = Key不同, oldModel有且newModel沒有
            //Update = Key相同, Value不同

            //new List<T>().AsReadOnly()
        }

    }
}
