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
                var wrapper = param as DynamicParametersWrapper;
                if (wrapper != null)
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
                var dictionary = param as IEnumerable<KeyValuePair<string, object>>;
                if (dictionary != null)
                {
                    Instance.AddDynamicParams(WrapDictionaryParam(dictionary));
                    return;
                }

                //一般model
                if (Templates == null) Templates = new List<object>();
                Templates.Add(param);
            }

            //將Dictionary的Value處理Enum轉換
            private static Dictionary<string, object> WrapDictionaryParam(IEnumerable<KeyValuePair<string, object>> dict)
            {
                return dict.ToDictionary(n => n.Key, n =>
                {
                    var value = n.Value;
                    if (value == null) return value;

                    var valueType = value.GetType();
                    var elemType = InternalHelper.GetElementType(valueType);
                    var isCollection = elemType != null;
                    if (elemType == null) elemType = valueType;
                    var nullableType = Nullable.GetUnderlyingType(elemType);
                    var isNullableType = nullableType != null;
                    if (nullableType != null) elemType = nullableType;
                    if (elemType.IsEnum)
                    {
                        var method = ModelWrapper.EnumInfo.Get(elemType).Metadata.GetConverter(isNullableType, isCollection);
                        return method.Invoke(null, new object[] { value });
                    }
                    return value;
                });
            }

#if DEBUG  //測試用而已
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
            Wrapper.AddDynamicParams(param);
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
