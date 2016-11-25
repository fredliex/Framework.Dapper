using System;
using Framework.Data;
using System.Data;
using System.Data.SqlClient;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using static Framework.Data.ModelWrapper;

namespace Framework.Test
{
    public sealed class DataTest
    {
        private static T GetCommand<T>(string sql, object param, CommandType commandType = CommandType.Text) where T : IDbCommand, new()
        {
            var command = new T();
            command.CommandType = commandType;
            command.CommandText = sql;
            command.Connection = new SqlConnection();


            var tmpParam = ModelWrapper.WrapParam(param, CommandType.Text, command.CommandText);
            var dynamicParameters = tmpParam as DynamicParameters.DynamicParametersWrapper;
            if (dynamicParameters != null)
            {
                dynamicParameters.AddParameters(command);
                return command;
            }

            var wraper = tmpParam as ModelWrapper.ParamWrapper;
            if (wraper != null)
            {
                wraper.ParamGenerator(command, param);
                return command;
            }


            throw new Exception("未處理");
        }

        #region 非IModel
        internal sealed class NonInterfaceModel
        {
            public string strCol1;
            public string strCol2 { get; set; }
            internal string strCol3;
            internal string strCol4 { get; set; }
            public StringEnum strEnum { get; set; }
        }
        [Fact(DisplayName = "無繼承IModel")]
        public void NonModelInterface()
        {
            var model = new NonInterfaceModel { strCol1 = "3", strCol2 = "4", strCol3 = "5", strCol4 = "6", strEnum = StringEnum.B };
            var sqlStr = @"select * from tabA where strCol1=@strCol1 and strCol2=@strCol2 and strCol3=@strCol3 and strCol4=@strCol4 and strEnum=@strEnum";
            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("strCol2", model.strCol2, DbType.String, 4000)
                .Verify("strEnum", "bb", DbType.String, 4000);
        }
        #endregion


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
            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(4, command.Parameters.Count);
            command.Parameters
                .Verify("strCol1", model.strCol1, DbType.String, 4000)
                .Verify("strCol2", model.strCol2, DbType.String, 4000)
                .Verify("strCol3", model.strCol3, DbType.String, 4000)
                .Verify("strCol4", model.strCol4, DbType.String, 4000);
        }
        #endregion

        #region EnumMapping
        public enum StringEnum
        {
            [Value("aa")]
            A,
            [Value("bb")]
            B,
            [Value("cc")]
            C
        }
        public enum NormalEnum
        {
            A = 1,
            B = 2,
            C = 3
        }
        public sealed class EnumMappingModel : IModel
        {
            public NormalEnum norEnum;
            public StringEnum strEnum;
            public string strCol;
            public int intCol;
            public decimal decimalCol;
        }


        [Fact(DisplayName = "列舉對應")]
        public void EnumMapping()
        {
            var model = new EnumMappingModel { norEnum = NormalEnum.B, strEnum = StringEnum.B, strCol = "abcd", intCol = 10, decimalCol = 10 };
            var sqlStr = @"select * from tabA where norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and decimalCol=@decimalCol";
            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(5, command.Parameters.Count);
            command.Parameters
                .Verify("norEnum", (int)model.norEnum, DbType.Int32)
                .Verify("strEnum", "bb", DbType.String, 4000)
                .Verify("strCol", "abcd", DbType.String, 4000)
                .Verify("intCol", model.intCol, DbType.Int32)
                .Verify("decimalCol", model.decimalCol, DbType.Decimal);
        }
        #endregion

        #region Nullable
        internal sealed class NullableModel : IModel
        {
            public NormalEnum? norEnum;
            public StringEnum? strEnum;
            public string strCol;
            public int? intCol;
            public decimal? decimalCol;
        }

        [Fact(DisplayName = "Nullable")]
        public void NullableTest()
        {
            var model = new NullableModel { norEnum = NormalEnum.B, strEnum = StringEnum.B, strCol = "abcd", intCol = 10, decimalCol = null };
            var sqlStr = @"select * from tabA where norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and decimalCol=@decimalCol";

            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(5, command.Parameters.Count);
            command.Parameters
                .Verify("norEnum", 2, DbType.Int32)
                .Verify("strEnum", "bb", DbType.String, 4000)
                .Verify("strCol", "abcd", DbType.String, 4000)
                .Verify("intCol", 10, DbType.Int32)
                .Verify("decimalCol", DBNull.Value, DbType.Decimal);


            model = new NullableModel();
            command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(5, command.Parameters.Count);
            command.Parameters
                .Verify("norEnum", DBNull.Value, DbType.Int32)
                .Verify("strEnum", DBNull.Value, DbType.String, 0)
                .Verify("strCol", DBNull.Value, DbType.String, 0)
                .Verify("intCol", DBNull.Value, DbType.Int32)
                .Verify("decimalCol", DBNull.Value, DbType.Decimal);
        }
        #endregion

        #region NullMapping
        public sealed class NullMappingModel : IModel
        {
            [Column(NullMapping = "A")]
            public NormalEnum? norEnum;
            [Column(NullMapping = 10)]
            public StringEnum? strEnum;
            [Column(NullMapping = 2D)] //docuble
            public string strCol;
            [Column(NullMapping = 3L)] //long
            public int? intCol;
            [Column(NullMapping = NormalEnum.A)]
            public decimal? decimalCol;
        }

        [Fact(DisplayName = "NullMapping")]
        public void NullMapping()
        {
            var model = new NullMappingModel();
            var sqlStr = @"select * from tabA where norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and decimalCol=@decimalCol";
            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(5, command.Parameters.Count);
            command.Parameters
                .Verify("norEnum", "A", DbType.String, 4000)
                .Verify("strEnum", 10, DbType.Int32)
                .Verify("strCol", 2D, DbType.Double)
                .Verify("intCol", 3L, DbType.Int64)
                .Verify("decimalCol", 1, DbType.Int32);
        }
        #endregion

        #region 集合
        [Fact(DisplayName = "集合")]
        public void EnumerableParam()
        {
            var sqlStr = @"select * from tabA col in @col";
            var command = GetCommand<SqlCommand>(sqlStr, new { col = new[] { "A", null, "C" } });
            Assert.Equal("select * from tabA col in (@col1,@col2,@col3)", command.CommandText);
            Assert.Equal(3, command.Parameters.Count);
            command.Parameters
                .Verify("col1", "A", DbType.String, 4000)
                .Verify("col2", DBNull.Value, DbType.String, 4000)   //總感覺這邊應該要是0才對,而不是4000
                .Verify("col3", "C", DbType.String, 4000);

            command = GetCommand<SqlCommand>(sqlStr, new { col = new StringEnum?[] { StringEnum.A, null, StringEnum.C } });
            Assert.Equal("select * from tabA col in (@col1,@col2,@col3)", command.CommandText);
            Assert.Equal(3, command.Parameters.Count);
            command.Parameters
                .Verify("col1", "aa", DbType.String, 4000)
                .Verify("col2", DBNull.Value, DbType.String, 4000)   //總感覺這邊應該要是0才對,而不是4000
                .Verify("col3", "cc", DbType.String, 4000);
        }
        #endregion

        private static Hashtable a = new Hashtable();
        private static Hashtable b = a;


        #region 
        [Fact(DisplayName = "字典")]
        public void DictionaryParam()
        {
            //一般資料
            var sqlStr = @"select * from tabA col1 = @col1 and col2 = @col2";
            var command = GetCommand<SqlCommand>(sqlStr, new Dictionary<string, object>
            {
                ["col1"] = "A",
                ["col2"] = "B",
            });
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("col1", "A", DbType.String, 4000)
                .Verify("col2", "B", DbType.String, 4000);

            //有EnumValue
            command = GetCommand<SqlCommand>(sqlStr, new Dictionary<string, object>
            {
                ["col1"] = StringEnum.A,
                ["col2"] = StringEnum.B,
            });
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("col1", "aa", DbType.String, 4000)
                .Verify("col2", "bb", DbType.String, 4000);

            //EnumValue的集合
            command = GetCommand<SqlCommand>("select * from tabA col in @col", new Dictionary<string, object> 
            {
                ["col"] = new[] { StringEnum.A, StringEnum.B }
            });
            Assert.Equal("select * from tabA col in (@col1,@col2)", command.CommandText);
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("col1", "aa", DbType.String, 4000)
                .Verify("col2", "bb", DbType.String, 4000);

            //DynamicParameters 
            var param = new DynamicParameters(new { col = StringEnum.A });
            command = GetCommand<SqlCommand>("select * from tabA col = @col", param);
            Assert.Equal(1, command.Parameters.Count);
            command.Parameters.Verify("col", "aa", DbType.String, 4000);

            //DynamicParameters Enumerable
            param = new DynamicParameters(new { col = new[] { StringEnum.A, StringEnum.B } });
            command = GetCommand<SqlCommand>("select * from tabA col in @col", param);
            Assert.Equal("select * from tabA col in (@col1,@col2)", command.CommandText);
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("col1", "aa", DbType.String, 4000)
                .Verify("col2", "bb", DbType.String, 4000);
        }
        #endregion

        [Fact(DisplayName = "aaaa")]
        public void aaaaa()
        {
            using (var conn = DbHelper.OpenConnection("test.local"))
            {
                var aa = conn.Query<NullMappingModel>("select '1' as strCol").ToList();
            }
        }
    }
}
