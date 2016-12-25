using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class Repository
    {
        internal sealed class ParameterMatedata
        {
            public string MemberName { get; private set; }
            public bool IsMulti { get; private set; }
            

            public static Dictionary<string, ParameterMatedata> Resolve(object parameter, TableInfo tableInfo = null)
            {
                var multiParams = parameter as IEnumerable;
                if (multiParams != null)
                {
                    if (multiParams is string) throw new ArgumentException("參數不可為字串");
                    parameter = multiParams.Cast<object>().FirstOrDefault();
                }
                ColumnInfo[] columns = null;
                if (parameter != null)
                {
                    var dict = multiParams as IEnumerable<KeyValuePair<string, object>>;
                    if (dict == null)
                    {
                        columns = TableInfo.Get(parameter.GetType()).Columns;
                    }
                    else
                    {
                        columns = dict.Select(n => ColumnInfo
                        {
                            
                        }).
                    }
                }



                /*

                ColumnInfo[] columns = null;
                if (parameter != null)
                {

                    var multiParams = parameter as IEnumerable;
                    if (multiParams == null)
                    {
                        columns = TableInfo.Get(parameter.GetType()).Columns;
                    }
                    else
                    {
                        var dict = multiParams as IEnumerable<KeyValuePair<string, object>>;
                        if (dict == null)
                        {
                            if (multiParams is string) throw new ArgumentException("參數不可為字串");
                            multiParams.GetEnumerator().
                            columns = TableInfo.Get(multiParams parameter.GetType()).Columns;
                        }
                    }


                }

                return new ParameterMatedata { Columns = columns ?? new ColumnInfo[0] };

                
                IEnumerable<object> parameters;
                


                if (multiParams != null && !(multiParams is IEnumerable<KeyValuePair<string, object>>))
                {
                    if (multiParams is string) throw new ArgumentException("data不可為字串");
                    datas = multiData.Cast<object>().Where(x => x != null);
                }
                else
                {
                    datas = new[] { data };
                }
                */

            }

        }

    }
}
