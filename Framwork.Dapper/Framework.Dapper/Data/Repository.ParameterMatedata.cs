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
            public bool IsMultiParameter { get; private set; }
            public ColumnInfoCollection Columns { get; private set; }

            internal ParameterMatedata(TableInfo modelTable, object parameter)
            {
                var multiParams = parameter as IEnumerable;
                if (multiParams != null)
                {
                    if (multiParams is string) throw new ArgumentException("參數不可為字串");
                    IsMultiParameter = true;
                    parameter = multiParams.Cast<object>().FirstOrDefault();
                }
                ColumnInfo[] columns = null;
                if (parameter != null)
                {
                    var parameterType = parameter.GetType();
                    if (parameterType == modelTable.Type)
                    {
                        Columns = modelTable.Columns;
                    }
                    else
                    {
                        var dict = multiParams as IEnumerable<KeyValuePair<string, object>>;
                        if (dict == null)
                        {
                            var paramColumns = TableInfo.Get(parameterType).Columns;
                        }

                    }

                    /*
                    var dict = multiParams as IEnumerable<KeyValuePair<string, object>>;
                    if (dict == null)
                    {
                        parameter columns = TableInfo.Get(parameter.GetType()).Columns;
                    }
                    else
                    {
                        columns = dict.Select(n => ColumnInfo
                        {

                        }).
                    }
                    */
                }
            }

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
