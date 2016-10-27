using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace Framework.Data
{
    //對Dapper.DynamicParameters作封裝
    public sealed class DynamicParameters
    {
        private sealed class DynamicParametersWrapper : SqlMapper.IDynamicParameters
        {
            private readonly Dapper.DynamicParameters instance = new Dapper.DynamicParameters();
            private List<object> templates;

            public void AddDynamicParams(object param)
            {
                if (param == null) return;
                var subDynamic = param as DynamicParametersWrapper;
                if (subDynamic != null)
                {
                    if (templates == null) templates = new List<object>();
                    templates.AddRange(subDynamic.templates);
                    instance.AddDynamicParams(subDynamic.instance);
                    return;
                }
                var dictionary = param as IEnumerable<KeyValuePair<string, object>>;
                if (dictionary != null)
                {
                    instance.AddDynamicParams(ModelWrapper.WrapDictionaryParam(dictionary));
                    return;
                }
                templates.Add(param);
            }

            public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
            {
                if (templates != null)
                {
                    foreach (var template in templates)
                    {
                        var paramGeneratorBuilder = new ModelWrapper.ParamGeneratorBuilder(template.GetType(), identity.commandType ?? CommandType.Text, identity.sql, false);
                        var paramGenerator = paramGeneratorBuilder.CreateGenerator();
                        paramGenerator(command, template);
                    }
                }
                ((SqlMapper.IDynamicParameters)instance).AddParameters(command, identity);
            }
        }

        private DynamicParametersWrapper wrapper = new DynamicParametersWrapper();

        public DynamicParameters() { }

        public DynamicParameters(object template)
        {
            AddDynamicParams(template);
        }

        public void AddDynamicParams(object param)
        {
            var wrapperDynamic = param as DynamicParameters;
            wrapper.AddDynamicParams(wrapperDynamic == null ? param : wrapperDynamic.wrapper);
        }
    }
}
