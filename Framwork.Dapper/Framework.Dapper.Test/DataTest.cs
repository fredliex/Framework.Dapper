using System;
using Framework.Data;
using System.Data;
using System.Data.SqlClient;
using Xunit;

namespace Framework.Test
{
    public sealed class DataTest
    {
        private static T GetCommand<T>(object param, CommandType commandType, string sql) where T : IDbCommand, new()
        {
            var command = new T();
            command.CommandType = commandType;
            command.CommandText = sql;
            var wraper = (ModelWrapper.ParamWrapper)ModelWrapper.WrapParam(param, CommandType.Text, command.CommandText);
            wraper.ParamGenerator(command, param);
            return command;
        }

        #region PublicInternal
        internal sealed class PublicInternalModel : IModel
        {
            public string strCol1;
            public string strCol2 { get; set; }
            [Column]
            internal string strCol3;
            [Column]
            internal string strCol4 { get; set; }

            [NonColumn]
            public string nonCol1;
            [NonColumn]
            public string nonCol2 { get; set; }

            internal string nonCol3;
            internal string nonCol4 { get; set; }
        }

        [Fact(DisplayName = "public 與 internal")]
        public void PublicInternal()
        {
            var model = new PublicInternalModel {
                strCol1 = "3", strCol2 = "4", strCol3 = "5", strCol4 = "6",
                nonCol1 = "a", nonCol2 = "b", nonCol3 = "c", nonCol4 = "d"
            };
            var sqlStr = @"select * from tabA where 
                strCol1=@strCol1 and strCol2=@strCol2 and strCol3=@strCol3 and strCol4=@strCol4 and
                nonCol1=@nonCol1 and nonCol2=@nonCol2 and nonCol3=@nonCol3 and nonCol4=@nonCol4";
            var command = GetCommand<SqlCommand>(model, CommandType.Text, sqlStr);
            Assert.Equal(4, command.Parameters.Count);
            command.Parameters
                .Verify("strCol1", model.strCol1, DbType.String, 4000)
                .Verify("strCol2", model.strCol2, DbType.String, 4000)
                .Verify("strCol3", model.strCol3, DbType.String, 4000)
                .Verify("strCol4", model.strCol4, DbType.String, 4000);
        }
        #endregion

        #region EnumMapping
        internal enum StringEnum
        {
            [Value("aa")]
            A,
            [Value("bb")]
            B,
            [Value("cc")]
            C
        }
        internal enum NormalEnum
        {
            A = 1,
            B = 2,
            C = 3
        }
        internal sealed class EnumMappingModel : IModel
        {
            public NormalEnum norEnum;
            public StringEnum strEnum;
            public string strCol;
            public int intCol;
            public decimal? decimalCol;
        }

        /*
         *#region
         * asdasdasd 
         *#endregion 
         * 
         */


        [Fact(DisplayName = "列舉對應")]
        public void EnumMapping()
        {
            var model = new EnumMappingModel { norEnum = NormalEnum.B, strEnum = StringEnum.B, strCol = "abcd", intCol = 10, decimalCol = null };
            var sqlStr = @"select * from tabA where norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and decimalCol=@decimalCol";
            var command = GetCommand<SqlCommand>(model, CommandType.Text, sqlStr);
            Assert.Equal(5, command.Parameters.Count);
            command.Parameters
                .Verify("norEnum", 2, DbType.Int16)
                .Verify("strEnum", "bb", DbType.String, 4000)
                .Verify("strCol", "abcd", DbType.String, 4000)
                .Verify("intCol", 10, DbType.Int16)
                .Verify("decimalCol", DBNull.Value, DbType.Decimal);
        }
        #endregion


        #region NullMapping
        internal sealed class NullMappingModel
        {

        }

        public void NullMapping()
        {


        }
        #endregion





    }
}
