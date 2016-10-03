using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Data;

namespace Framework.Data
{
    partial class ModelWrapper
    {
        internal sealed class ParamWrapper : SqlMapper.IDynamicParameters
        {
            internal object Model;
            internal Action<IDbCommand, object> ParamInfoGenerator;

            void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
            {
                ParamInfoGenerator(command, Model);
            }
        }
    }
}
