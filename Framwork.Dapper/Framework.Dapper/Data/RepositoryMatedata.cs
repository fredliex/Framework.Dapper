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
        public IEnumerable<ColumnInfo> ParamColumns { get; private set; }

        public string SqlStr;

        public RepositoryMatedata(object model, object param)
        {
            Model = model;
            Param = param;
            ParamColumns = ResolveParamColumns(param);
        }

        private static IEnumerable<ColumnInfo> ResolveParamColumns(object param)
        {
            var dict = param as IDictionary<string, object>;
            if (dict != null) return ColumnInfo.Resolve(new[] { dict });

            var dicts = param as IEnumerable<IDictionary<string, object>>;
            if (dicts != null) return ColumnInfo.Resolve(dicts);

            var paramType = param.GetType();
            var elemType = InternalHelper.GetElementType(paramType);

            return ModelTableInfo.Get(elemType ?? paramType).Columns;
        }
    }
}
