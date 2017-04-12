using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Framework.Data
{
    internal static class ModelMergerHelper
    {

    }

    public partial struct ModelMerger<T> where T : IDataModel
    {
        private static IEqualityComparer<T> keyComparer;
        private static IEqualityComparer<T> valueComparer; 

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
            


            /*
            //先比較key
            Enumerable
            var newKeys = new Dictionary<T>(newModels, keyComparer);
            oldModels.Select(oldKey =>
            {
                var newKey = newKeys
            new ValueTuple<T, T>(old, newKeys))
            }
            





            
            //new Dictionary<string, T>(IEqualityComparer)
            oldModels.Except(newModels); //與oldModels中排除newModels
            oldModels.Intersect(newModels); //回傳交集
            oldModels.Union(newModels); //回傳聯集

            oldModels
            oldModels.Join()

            oldModels.GroupJoin()
            */


            //Insert = Key不同, oldModel沒有且newModel有
            //Delete = Key不同, oldModel有且newModel沒有
            //Update = Key相同, Value不同
            //Same = Key相同, Value相同

            //new List<T>().AsReadOnly()
        }

    }
}
