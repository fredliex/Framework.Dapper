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

            internal ParameterMatedata(object parameter)
            {
                //如果是集合參數的話, 抓第一個
                var multiParams = parameter as IEnumerable;
                if (multiParams != null && !(parameter is string || parameter is IEnumerable<KeyValuePair<string, object>> || parameter is DynamicParameters))
                {
                    parameter = multiParams.Cast<object>().FirstOrDefault();
                    IsMultiParameter = true;
                }





                var dict = parameter as IEnumerable<KeyValuePair<string, object>>;
                if (dict != null) 
                {
                    Columns = new ColumnInfoCollection(dict);
                }


                var multiParams = parameter as IEnumerable;

                if (multiParams != null)
                {
                    if (multiParams is string) throw new ArgumentException("參數不可為字串");
                    IsMultiParameter = true;
                    parameter = multiParams.Cast<object>().FirstOrDefault();
                }
                if (parameter != null)
                {
                    var parameterType = parameter.GetType();
                    var dict = multiParams as IEnumerable<KeyValuePair<string, object>>;
                    if (dict == null)
                    {
                        var paramColumns = TableInfo.Get(parameterType).Columns;
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

            private static ColumnInfoCollection ResolveColumns(object param, out bool isMultiParameter)
            {
                isMultiParameter = false;

                //如果是集合參數的話, 抓第一個
                var multiParams = param as IEnumerable;
                if (multiParams != null && !(param is string || param is IEnumerable<KeyValuePair<string, object>> || param is DynamicParameters))
                {
                    param = multiParams.Cast<object>().FirstOrDefault();
                    isMultiParameter = true;
                }

                //如果是DynamicParameters的話

                //如果是Dictionar<string, object>的話
                var dictParams = param as IEnumerable<KeyValuePair<string, object>>;
                if (dictParams != null) return new ColumnInfoCollection(dictParams);





                IEnumerable<KeyValuePair<string, object>> dictParams = null;
                if (multiParams != null)
                {
                    dictParams = parameter as IEnumerable<KeyValuePair<string, object>>;
                    if()

                    if (multiParams is string) throw new ArgumentException("參數不可為字串");


                    IsMultiParameter = true;
                    parameter = multiParams.Cast<object>().FirstOrDefault();
                }


                //判斷為
                if (dict != null) return new ColumnInfoCollection(dict);


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
