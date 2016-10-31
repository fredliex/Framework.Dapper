using Dapper;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace Framework.Data
{

    public static partial class ModelWrapper
    {
        private static ConcurrentDictionary<Type, TableInfo> tableInfoCache = new ConcurrentDictionary<Type, TableInfo>();

        /// <summary>取得Table資訊</summary>
        private static TableInfo GetTableInfo(Type modelType)
        {
            return tableInfoCache.GetOrAdd(modelType, t => new TableInfo(t));
        }

        internal static object WrapParam(object param, CommandType commandType, string sql)
        {
            if (param is IDynamicParameters) return param;
            var dynamicParameters = param as DynamicParameters;
            if (dynamicParameters != null) return dynamicParameters.Wrapper;
            var dict = param as IEnumerable<KeyValuePair<string, object>>;
            if (dict != null) return new DynamicParameters(dict).Wrapper;
            //if (dict != null) return WrapDictionaryParam(dict);
            var paramGeneratorBuilder = new ParamGeneratorBuilder(param.GetType(), commandType, sql, false);
            var paramGenerator = paramGeneratorBuilder.CreateGenerator();
            var models = param as IEnumerable;
            if (models != null && !(param is string || param is IEnumerable<KeyValuePair<string, object>>)) return new EnumerableParamWrapper(models, paramGenerator);
            return new ParamWrapper { Model = param, ParamGenerator = paramGenerator };
        }

        /*
        internal static Dictionary<string, object> WrapDictionaryParam(IEnumerable<KeyValuePair<string, object>> dict)
        {
            return dict.ToDictionary(n => n.Key, n =>
            {
                var value = n.Value;
                if (value == null) return value;
                var list = value as IEnumerable;
                Type valueType;
                var method =
                    list == null ? EnumValueHelper.GetValueGetterMethod(value.GetType(), out valueType) :
                    !(list is string) ? EnumValueHelper.GetValuesGetterMethod(value.GetType(), out valueType) :
                    null;
                return method == null ? value : method.Invoke(null, new object[] { value });
            });
        }
        */

    }
}
