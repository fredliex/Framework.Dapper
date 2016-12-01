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

            //傳入參數 依照commandType + sql + conn + param.GetType() 當作識別key


            if (param == null || param is IDynamicParameters) return param;

            //DynamicParameters的話就回wrapper
            var dynamicParameters = param as DynamicParameters;
            if (dynamicParameters != null) return dynamicParameters.Wrapper;

            //Dictionary<string, object>的話丟給DynamicParameters處理
            var dict = param as IEnumerable<KeyValuePair<string, object>>;
            if (dict != null) return new DynamicParameters(dict).Wrapper;

            //由ParamWrapper處理
            var paramGeneratorBuilder = new ParamGeneratorBuilder(param.GetType(), commandType, sql, false);
            var paramGenerator = paramGeneratorBuilder.CreateGenerator();
            var models = param as IEnumerable;
            if (models != null && !(param is string || param is IEnumerable<KeyValuePair<string, object>>)) return new EnumerableParamWrapper(models, paramGenerator);
            return new ParamWrapper { Model = param, ParamGenerator = paramGenerator };
        }
        
    }
}
