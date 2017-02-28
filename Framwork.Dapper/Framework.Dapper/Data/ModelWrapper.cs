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

        internal static object WrapParam(IDbConnection conn, object param, CommandType commandType, string sql, out Cache cache)
        {
            var paramType = param?.GetType();

            //傳入參數 依照commandType + sql + conn + param.GetType() 當作識別key
            cache = Cache.GetCache(conn, commandType, sql, paramType, () =>
            {
                //沒參數或是IDynamicParameters的話, 不包裝
                if (param == null || param is IDynamicParameters) return p => p;

                //DynamicParameters的話就回wrapper
                if (param is DynamicParameters) return p => ((DynamicParameters)p).Wrapper;

                //Dictionary<string, object>的話丟給DynamicParameters處理, DynamicParameters內部會處理Enum轉換
                if (param is IEnumerable<KeyValuePair<string, object>>) return p => new DynamicParameters(p).Wrapper;

                //由ParamWrapper處理
                var paramGeneratorBuilder = new ParamGeneratorBuilder(paramType, commandType, sql, false);
                var paramGenerator = paramGeneratorBuilder.CreateGenerator();
                return param is IEnumerable && !(param is string) ?
                    new Func<object, object>(p => new EnumerableParamWrapper((IEnumerable)p, paramGenerator)) :
                    new Func<object, object>(p => new ParamWrapper { Model = p, ParamGenerator = paramGenerator });
            });

            return cache.ParamWrapper(param);
        }
    }
}
