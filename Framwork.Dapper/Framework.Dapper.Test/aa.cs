using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Framework.Test.DataTest;
using static Framework.Test.RepositoryTest;

namespace Framework.Dapper.Test
{
    public class aa
    {
        public object gg(IDataReader reader)
        {
            var model = new MemberDefaultModel();
            model.colIntNull = null;
            model.colIntNull2 = null;
            Console.WriteLine(model);
            return model;
        }
    }
}
