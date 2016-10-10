using Framework.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Framework.Test.DataTest;

namespace Framework.Dapper.Test
{
    class AA
    {
        public static void DynamicCreate(IDbCommand dbCommand, object obj)
        {
            NullableModel nullableModel = (NullableModel)obj;
            IList expr_0D = dbCommand.Parameters;
            IDbDataParameter expr_14 = dbCommand.CreateParameter();
            expr_14.ParameterName = "norEnum";
            expr_14.Direction = ParameterDirection.Input;
            object expr_32 = nullableModel.norEnum;
            expr_14.Value = ((expr_32 != null) ? (object)(NormalEnum)expr_32 : DBNull.Value);
            expr_14.DbType = DbType.Int32;
            expr_0D.Add(expr_14);
        }
    }
}
