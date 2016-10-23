using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    //對Dapper.DynamicParameters作封裝
    public sealed class DynamicParameters
    {
        private readonly Dapper.DynamicParameters instance = new Dapper.DynamicParameters();

        public DynamicParameters()
        {
        }

        public DynamicParameters(object template)
        {
            AddDynamicParams(template);
        }

        public void AddDynamicParams(object param)
        {
            if (param == null) return;
            var subDynamic = param as DynamicParameters;
            if (subDynamic != null)
            {
                instance.AddDynamicParams(subDynamic.instance);
                return;
            }
            var dictionary = param as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                instance.AddDynamicParams(ModelWrapper.WrapDictionaryParam(dictionary));
                return;
            }








        }
    }
}
