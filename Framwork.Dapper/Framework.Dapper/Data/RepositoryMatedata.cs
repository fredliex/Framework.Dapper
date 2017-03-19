using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    internal sealed class RepositoryMatedata
    {
        public object Model { get; private set; }
        public object Filter { get; private set; }
        public object Param;
        //private Type paramElemType;
        //private Action<IDictionary<string, object>, object> dictionaryFiller = null;
        /// <summary>如果Param是null的話，ParamColumns也會是null。</summary>
        public IEnumerable<ColumnInfo> FilterColumns { get; private set; }

        public string SqlStr;

        public RepositoryMatedata(object model, object filter)
        {
            Model = model;
            Filter = filter;
            FilterColumns = ResolveFilterColumns(filter, out Type paramElemType);
        }

        private static IEnumerable<ColumnInfo> ResolveFilterColumns(object filter, out Type elemType)
        {
            elemType = null;
            if (filter == null) return null;

            if (filter is IDictionary<string, object> dict)
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

            var paramType = filter.GetType();
            elemType = InternalHelper.GetElementType(paramType) ?? paramType;

            return ModelTableInfo.Get(elemType).Columns;
        }
    }
}
