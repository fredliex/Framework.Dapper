using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Reflection;
using System.Collections;
using System.Data.Common;

namespace Framework.Data
{
    //對Dapper.DynamicParameters作封裝
    public sealed class DynamicParameters
    {
        internal sealed class DynamicParametersWrapper : SqlMapper.IDynamicParameters
        {
            internal readonly Dapper.DynamicParameters Instance = new Dapper.DynamicParameters();
            internal List<object> Templates = null; //同DynamicParameters裡面的Templates, 這邊放另外一個副本是因為DynamicParameters.Templates為內部欄位而無法直接取用

            public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
            {
                if (Templates != null)
                {
                    foreach (var template in Templates)
                    {
                        var paramGeneratorBuilder = new ModelWrapper.ParamGeneratorBuilder(template.GetType(), identity.commandType ?? CommandType.Text, identity.sql, false);
                        var paramGenerator = paramGeneratorBuilder.CreateGenerator();
                        paramGenerator(command, template);
                    }
                }
                ((SqlMapper.IDynamicParameters)Instance).AddParameters(command, identity);
            }

            public void AddDynamicParams(object param)
            {
                //同是DynamicParametersWrapper
                if (param is DynamicParametersWrapper wrapper)
                {
                    if (wrapper.Templates != null)
                    {
                        if (Templates == null) Templates = new List<object>();
                        Templates.AddRange(wrapper.Templates);
                    }
                    Instance.AddDynamicParams(wrapper.Instance);
                    return;
                }

                //是Dictionary, 須額外處理EnumMapping
                if (param is IEnumerable<KeyValuePair<string, object>> dictionary)
                {
                    Instance.AddDynamicParams(InternalHelper.WrapDictionaryParam(dictionary));
                    return;
                }

                //一般model
                if (Templates == null) Templates = new List<object>();
                Templates.Add(param);
            }

        }

        internal readonly DynamicParametersWrapper Wrapper = new DynamicParametersWrapper();

        public DynamicParameters() { }

        public DynamicParameters(object template) => AddDynamicParams(template);

        public void AddDynamicParams(object param)
        {
            if (param == null) return;
            Wrapper.AddDynamicParams(param);
        }

        public void Add(string name, object value, DbType? dbType, ParameterDirection? direction, int? size) =>
            Wrapper.Instance.Add(name, value, dbType, direction, size);

        public void Add(string name, object value = null, DbType? dbType = null, ParameterDirection? direction = null, int? size = null, byte? precision = null, byte? scale = null) =>
            Wrapper.Instance.Add(name, value, dbType, direction, size, precision, scale);

        public IEnumerable<string> ParameterNames => Wrapper.Instance.ParameterNames;

        public T Get<T>(string name) => Wrapper.Instance.Get<T>(name);
    }
}
