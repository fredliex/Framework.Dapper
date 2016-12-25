using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class Repository
    {
        internal sealed class ParameterColumnInfo : ColumnInfo
        {
            internal override MemberInfo Member
            {
                get { throw new NotImplementedException(); }
            }

            internal override MemberTypes MemberType
            {
                get { throw new NotImplementedException(); }
            }

            internal override void EmitGenerateGet(ILGenerator il)
            {
                throw new NotImplementedException();
            }

            internal override void EmitGenerateSet(ILGenerator il)
            {
                throw new NotImplementedException();
            }
        }

    }
}
