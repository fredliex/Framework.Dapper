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
            internal readonly Dapper.DynamicParameters instance = new Dapper.DynamicParameters();
            internal List<object> templates = null;

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
            if (param == null) return;
            var subDynamic = param as DynamicParameters;
            if (subDynamic != null)
            {
                if (subDynamic.wrapper.templates != null)
                {
                    if (wrapper.templates == null) wrapper.templates = new List<object>();
                    wrapper.templates.AddRange(subDynamic.wrapper.templates);
                }
                wrapper.instance.AddDynamicParams(subDynamic.wrapper.instance);
                return;
            }
            var dictionary = param as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                wrapper.instance.AddDynamicParams(ModelWrapper.WrapDictionaryParam(dictionary));
                return;
            }
            if (wrapper.templates == null) wrapper.templates = new List<object>();
            wrapper.templates.Add(param);
        }

        public void Add(string name, object value, DbType? dbType, ParameterDirection? direction, int? size)
        {
            wrapper.instance.Add(name, value, dbType, direction, size);
        }

        public void Add(string name, object value = null, DbType? dbType = null, ParameterDirection? direction = null, int? size = null, byte? precision = null, byte? scale = null)
        {
            wrapper.instance.Add(name, value, dbType, direction, size, precision, scale);
        }

        public IEnumerable<string> ParameterNames => wrapper.instance.ParameterNames;

        public T Get<T>(string name)
        {
            return wrapper.instance.Get<T>(name);
        }
    }
}
