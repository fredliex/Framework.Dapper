using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Reflection;
using System.Collections;

namespace Framework.Data
{
    //對Dapper.DynamicParameters作封裝
    public sealed class DynamicParameters
    {
        internal sealed class DynamicParametersWrapper : SqlMapper.IDynamicParameters
        {
            internal readonly Dapper.DynamicParameters Instance = new Dapper.DynamicParameters();
            internal List<object> Templates = null;

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

#if DEBUG
            public void AddParameters(IDbCommand command)
            {
                var constructorParamTypes = new[] { typeof(string), typeof(CommandType?), typeof(IDbConnection), typeof(Type), typeof(Type), typeof(Type[]) };
                var constructorParams = new object[] { command.CommandText, command.CommandType, command.Connection, null, this.GetType(), null };
                var identity = (SqlMapper.Identity)typeof(SqlMapper.Identity).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, constructorParamTypes, null).Invoke(constructorParams);
                AddParameters(command, identity);
            }
#endif
        }

        internal readonly DynamicParametersWrapper Wrapper = new DynamicParametersWrapper();

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
                if (subDynamic.Wrapper.Templates != null)
                {
                    if (Wrapper.Templates == null) Wrapper.Templates = new List<object>();
                    Wrapper.Templates.AddRange(subDynamic.Wrapper.Templates);
                }
                Wrapper.Instance.AddDynamicParams(subDynamic.Wrapper.Instance);
                return;
            }
            var dictionary = param as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                Wrapper.Instance.AddDynamicParams(WrapDictionaryParam(dictionary));
                return;
            }
            if (Wrapper.Templates == null) Wrapper.Templates = new List<object>();
            Wrapper.Templates.Add(param);
        }

        private static Dictionary<string, object> WrapDictionaryParam(IEnumerable<KeyValuePair<string, object>> dict)
        {
            return dict.ToDictionary(n => n.Key, n =>
            {
                var value = n.Value;
                if (value == null) return value;
                var list = value as IEnumerable;
                Type valueType;
                var method =
                    list == null ? ModelWrapper.EnumValueHelper.GetValueGetterMethod(value.GetType(), out valueType) :
                    !(list is string) ? ModelWrapper.EnumValueHelper.GetValuesGetterMethod(value.GetType(), out valueType) :
                    null;
                return method == null ? value : method.Invoke(null, new object[] { value });
            });
        }

        public void Add(string name, object value, DbType? dbType, ParameterDirection? direction, int? size)
        {
            Wrapper.Instance.Add(name, value, dbType, direction, size);
        }

        public void Add(string name, object value = null, DbType? dbType = null, ParameterDirection? direction = null, int? size = null, byte? precision = null, byte? scale = null)
        {
            Wrapper.Instance.Add(name, value, dbType, direction, size, precision, scale);
        }

        public IEnumerable<string> ParameterNames => Wrapper.Instance.ParameterNames;

        public T Get<T>(string name)
        {
            return Wrapper.Instance.Get<T>(name);
        }
    }
}
