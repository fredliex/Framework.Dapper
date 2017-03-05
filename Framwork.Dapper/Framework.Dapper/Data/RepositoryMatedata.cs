using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class RepositoryMatedata
    {
        public object Model;
        public object Param;
        private Type paramElemType;
        private Action<IDictionary<string, object>, object> dictionaryFiller = null;
        /// <summary>如果Param是null的話，ParamColumns也會是null。</summary>
        public IEnumerable<ColumnInfo> ParamColumns { get; private set; }

        public string SqlStr;

        public RepositoryMatedata(object model, object param)
        {
            Model = model;
            Param = param;
            ParamColumns = ResolveParamColumns(param, out paramElemType);
        }

        private static IEnumerable<ColumnInfo> ResolveParamColumns(object param, out Type elemType)
        {
            elemType = null;
            if (param == null) return null;

            var dict = param as IDictionary<string, object>;
            if (dict != null)
            {
                elemType = dict.GetType();
                return ColumnInfo.Resolve(new[] { dict });
            }

            /*
            var dicts = param as IEnumerable<IDictionary<string, object>>;
            if (dicts != null)
            {
                elemType = dicts.FirstOrDefault()?.GetType();
                return ColumnInfo.Resolve(dicts);
            }
            */

            var paramType = param.GetType();
            elemType = InternalHelper.GetElementType(paramType) ?? paramType;

            return ModelTableInfo.Get(elemType).Columns;
        }
    }
}
