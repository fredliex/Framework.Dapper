using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    public static class DataModelHelper
    {
        private static ConcurrentDictionary<Type, Action<IDictionary<string, object>, object>> cacheDictionaryFillers =
            new ConcurrentDictionary<Type, Action<IDictionary<string, object>, object>>();

        private static Dictionary<string, object> ModelToDictionary(object model)
        {
            var filler = cacheDictionaryFillers.GetOrAdd(model.GetType(), ModelColumnInfo.GenerateDictionaryFiller);
            var dict = new Dictionary<string, object>();
            filler(dict, model);
            return dict;
        }

        public static Dictionary<string, object> ToDictionary(this IDataModel model)
        {
            if (model == null) return null;
            return ModelToDictionary(model);
        }

        public static IDictionary<string, object> ToDictionary(object model)
        {
            if (model == null) return null;

            if (model is IDictionary<string, object> dict) return InternalHelper.WrapDictionaryParam(dict);

            return ModelToDictionary(model);
        }
    }
}
