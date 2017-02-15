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

            ModelWrapper.Cache cache;
            var tmpParam = ModelWrapper.WrapParam(command.Connection, param, CommandType.Text, command.CommandText, out cache);
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

        private static T QueryData<T>(string sql)
        {
            using (var conn = DbHelper.OpenConnection("test.local"))
            {
                return conn.Query<T>(sql).FirstOrDefault();
            }
        }

        #region 非IDataModel物件
        internal sealed class NonInterfaceClass
        {
            public string strCol1;
            public string strCol2 { get; set; }
            internal string strCol3;
            internal string strCol4 { get; set; }
            public StringEnum strEnum { get; set; }
        }
        [Fact(DisplayName = "非IDataModel物件-參數")]
        public void NonInterfaceClassParam()
        {
            var model = new NonInterfaceClass { strCol1 = "3", strCol2 = "4", strCol3 = "5", strCol4 = "6", strEnum = StringEnum.B };
            var sqlStr = @"select * from tabA where strCol1=@strCol1 and strCol2=@strCol2 and strCol3=@strCol3 and strCol4=@strCol4 and strEnum=@strEnum";
            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("strCol2", model.strCol2, DbType.String, 4000)
                .Verify("strEnum", "bb", DbType.String, 4000);
        }


        [Fact(DisplayName = "非IDataModel物件-查詢")]
        public void NonInterfaceClassQuery()
        {
            var model = QueryData<NonInterfaceClass>("select 'a' strCol1, 'b' strCol2, 'c' strCol3, 'd' strCol4, 'bb' strEnum");
            Assert.Equal(null, model.strCol1);
            Assert.Equal("b", model.strCol2);
            Assert.Equal(null, model.strCol3);
            Assert.Equal(null, model.strCol4);
            Assert.Equal(StringEnum.B, model.strEnum);

            model = QueryData<NonInterfaceClass>("select null strCol1, null strCol2, null strCol3, null strCol4, null strEnum");
            Assert.Equal(null, model.strCol1);
            Assert.Equal(null, model.strCol2);
            Assert.Equal(null, model.strCol3);
            Assert.Equal(null, model.strCol4);
            Assert.Equal(default(StringEnum), model.strEnum);
        }
        #endregion

        #region 非IDataModel結構
        internal struct NonInterfaceStruct
        {
            public string strCol1;
            public string strCol2 { get; set; }
            internal string strCol3;
            internal string strCol4 { get; set; }
            public StringEnum strEnum { get; set; }
        }
        [Fact(DisplayName = "非IDataModel結構-參數")]
        public void NonInterfaceStructParam()
        {
            var model = new NonInterfaceStruct { strCol1 = "3", strCol2 = "4", strCol3 = "5", strCol4 = "6", strEnum = StringEnum.B };
            var sqlStr = @"select * from tabA where strCol1=@strCol1 and strCol2=@strCol2 and strCol3=@strCol3 and strCol4=@strCol4 and strEnum=@strEnum";
            var command = GetCommand<SqlCommand>(sqlStr, model);
            Assert.Equal(2, command.Parameters.Count);
            command.Parameters
                .Verify("strCol2", model.strCol2, DbType.String, 4000)
                .Verify("strEnum", "bb", DbType.String, 4000);
        }


        [Fact(DisplayName = "非IDataModel結構-查詢")]
        public void NonInterfaceStructQuery()
        {
            var model = QueryData<NonInterfaceStruct>("select 'a' strCol1, 'b' strCol2, 'c' strCol3, 'd' strCol4, 'bb' strEnum");
            Assert.Equal(null, model.strCol1);
            Assert.Equal("b", model.strCol2);
            Assert.Equal(null, model.strCol3);
            Assert.Equal(null, model.strCol4);
            Assert.Equal(StringEnum.B, model.strEnum);

            model = QueryData<NonInterfaceStruct>("select null strCol1, null strCol2, null strCol3, null strCol4, null strEnum");
            Assert.Equal(null, model.strCol1);
            Assert.Equal(null, model.strCol2);
            Assert.Equal(null, model.strCol3);
            Assert.Equal(null, model.strCol4);
            Assert.Equal(default(StringEnum), model.strEnum);
        }
        #endregion

        #region PublicInternalModel
        internal sealed class PublicInternalModel : IDataModel
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

        [Fact(DisplayName = "public 與 internal物件-參數")]
        public void PublicInternalParam()
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

        [Fact(DisplayName = "public 與 internal物件-查詢")]
        public void PublicInternalQuery()
        {
            var model = QueryData<PublicInternalModel>("select 'a' strCol1, 'b' strCol2, 'c' strCol3, 'd' strCol4, 'a1' nonCol1, 'a2' nonCol2, 'a3' nonCol3, 'a4' nonCol4");
            Assert.Equal("a", model.strCol1);
            Assert.Equal("b", model.strCol2);
            Assert.Equal("c", model.strCol3);
            Assert.Equal("d", model.strCol4);
            Assert.Equal(null, model.nonCol1);
            Assert.Equal(null, model.nonCol2);
            Assert.Equal(null, model.nonCol3);
            Assert.Equal(null, model.nonCol4);

            model = QueryData<PublicInternalModel>("select null strCol1, null strCol2, null strCol3, null strCol4, null nonCol1, null nonCol2, null nonCol3, null nonCol4");
            Assert.Equal(null, model.strCol1);
            Assert.Equal(null, model.strCol2);
            Assert.Equal(null, model.strCol3);
            Assert.Equal(null, model.strCol4);
            Assert.Equal(null, model.nonCol1);
            Assert.Equal(null, model.nonCol2);
            Assert.Equal(null, model.nonCol3);
            Assert.Equal(null, model.nonCol4);
        }
        #endregion

        #region PublicInternalStruct
        internal struct PublicInternalStruct : IDataModel
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

        [Fact(DisplayName = "public 與 internal結構-參數")]
        public void PublicInternalStructParam()
        {
            var model = new PublicInternalStruct
            {
                strCol1 = "3",
                strCol2 = "4",
                strCol3 = "5",
                strCol4 = "6",
                nonCol1 = "a",
                nonCol2 = "b",
                nonCol3 = "c",
                nonCol4 = "d"
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

        [Fact(DisplayName = "public 與 internal結構-查詢")]
        public void PublicInternalStructQuery()
        {
            var model = QueryData<PublicInternalStruct>("select 'a' strCol1, 'b' strCol2, 'c' strCol3, 'd' strCol4, 'a1' nonCol1, 'a2' nonCol2, 'a3' nonCol3, 'a4' nonCol4");
            Assert.Equal("a", model.strCol1);
            Assert.Equal("b", model.strCol2);
            Assert.Equal("c", model.strCol3);
            Assert.Equal("d", model.strCol4);
            Assert.Equal(null, model.nonCol1);
            Assert.Equal(null, model.nonCol2);
            Assert.Equal(null, model.nonCol3);
            Assert.Equal(null, model.nonCol4);

            model = QueryData<PublicInternalStruct>("select null strCol1, null strCol2, null strCol3, null strCol4, null nonCol1, null nonCol2, null nonCol3, null nonCol4");
            Assert.Equal(null, model.strCol1);
            Assert.Equal(null, model.strCol2);
            Assert.Equal(null, model.strCol3);
            Assert.Equal(null, model.strCol4);
            Assert.Equal(null, model.nonCol1);
            Assert.Equal(null, model.nonCol2);
            Assert.Equal(null, model.nonCol3);
            Assert.Equal(null, model.nonCol4);
        }
        #endregion


        #region EnumMapping
        public enum StringEnum
        {
            [DbValue("aa")]
            A,
            [DbValue("bb")]
            B,
            [DbValue("cc")]
            C
        }
        public enum NormalEnum
        {
            A = 1,
            B = 2,
            C = 3
        }
        public sealed class EnumMappingModel : IDataModel
        {
            public NormalEnum norEnum;
            public StringEnum strEnum;
            public string strCol;
            public int intCol;
            public decimal decimalCol;
        }


        [Fact(DisplayName = "列舉對應-參數")]
        public void EnumMappingParam()
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

        [Fact(DisplayName = "列舉對應-查詢")]
        public void EnumMappingQuery()
        {
            var model = QueryData<EnumMappingModel>("select 2 norEnum, 'bb' strEnum, 'c' strCol, 1 intCol, 1.5 decimalCol");
            Assert.Equal((NormalEnum)2, model.norEnum);
            Assert.Equal(StringEnum.B, model.strEnum);
            Assert.Equal("c", model.strCol);
            Assert.Equal(1, model.intCol);
            Assert.Equal(1.5m, model.decimalCol);

            model = QueryData<EnumMappingModel>("select null norEnum, null strEnum, null strCol, null intCol, null decimalCol");
            Assert.Equal(default(NormalEnum), model.norEnum);
            Assert.Equal(default(StringEnum), model.strEnum);
            Assert.Equal(null, model.strCol);
            Assert.Equal(default(int), model.intCol);
            Assert.Equal(default(decimal), model.decimalCol);
        }

        #endregion

        #region Nullable
        internal sealed class NullableModel : IDataModel
        {
            public NormalEnum? norEnum;
            public StringEnum? strEnum;
            public string strCol;
            public int? intCol;
            public decimal? decimalCol;
        }

        [Fact(DisplayName = "Nullable-參數")]
        public void NullableParam()
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

        [Fact(DisplayName = "Nullable-查詢")]
        public void NullableQuery()
        {
            var model = QueryData<NullableModel>("select 2 norEnum, 'bb' strEnum, 'c' strCol, 1 intCol, 1.5 decimalCol");
            Assert.Equal((NormalEnum)2, model.norEnum);
            Assert.Equal(StringEnum.B, model.strEnum);
            Assert.Equal("c", model.strCol);
            Assert.Equal(1, model.intCol);
            Assert.Equal(1.5m, model.decimalCol);

            model = QueryData<NullableModel>("select null norEnum, null strEnum, null strCol, null intCol, null decimalCol");
            Assert.Equal(null, model.norEnum);
            Assert.Equal(null, model.strEnum);
            Assert.Equal(null, model.strCol);
            Assert.Equal(null, model.intCol);
            Assert.Equal(null, model.decimalCol);
        }
        #endregion

        #region NullMapping
        public sealed class NullMappingModel : IDataModel
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

        [Fact(DisplayName = "NullMapping-參數")]
        public void NullMappingParam()
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

        [Fact(DisplayName = "NullMapping-查詢")]
        public void NullMappingQuery()
        {
            var model = QueryData<NullMappingModel>("select 3 norEnum, 'cc' strEnum, '4' strCol, 10 intCol, 5 decimalCol");
            Assert.Equal(NormalEnum.C, model.norEnum);
            Assert.Equal(StringEnum.C, model.strEnum);
            Assert.Equal("4", model.strCol);
            Assert.Equal(10, model.intCol);
            Assert.Equal(5, model.decimalCol);

            model = QueryData<NullMappingModel>("select 'A' norEnum, 10 strEnum, 2 strCol, 3 intCol, 1 decimalCol");
            Assert.Equal(null, model.norEnum);
            Assert.Equal(null, model.strEnum);
            Assert.Equal(null, model.strCol);
            Assert.Equal(null, model.intCol);
            Assert.Equal(null, model.decimalCol);

            model = QueryData<NullMappingModel>("select null norEnum, null strEnum, null strCol, null intCol, null decimalCol");
            Assert.Equal(null, model.norEnum);
            Assert.Equal(null, model.strEnum);
            Assert.Equal(null, model.strCol);
            Assert.Equal(null, model.intCol);
            Assert.Equal(null, model.decimalCol);
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

        private static IEqualityComparer equalityComparer = EqualityComparer<Type[]>.Default;

        #region 單值查詢
        [Fact(DisplayName = "單值查詢")]
        public void QueryValue()
        {
            var node = new[] { typeof(int), typeof(int), typeof(int) };

            var dictA = new HashSet<Type[]>();
            dictA.Add(new[] { typeof(int), typeof(int), typeof(int) });

            var dictB = new HashSet<int>();
            dictB.Add(node.GetHashCode());

            var dictC = new HashSet<int>();
            dictC.Add(StructuralComparisons.StructuralEqualityComparer.GetHashCode(node));

            var watch = new Stopwatch();

            watch.Restart();
            for (var i = 0; i < 10000; i++) dictA.Contains(node);
            Trace.WriteLine(watch.ElapsedTicks);

            watch.Restart();
            for (var i = 0; i < 10000; i++)
            {
                var hashCode = 17;
                foreach (var t in node)
                {
                    hashCode = hashCode * 23 + (t?.GetHashCode() ?? 0);
                }
                dictB.Contains(hashCode);
            };
            Trace.WriteLine(watch.ElapsedTicks);

            watch.Restart();
            for (var i = 0; i < 10000; i++) dictC.Contains(StructuralComparisons.StructuralEqualityComparer.GetHashCode(node));
            Trace.WriteLine(watch.ElapsedTicks);

            return;
            Assert.Equal(3, QueryData<int>("select 3"));
            Assert.Equal("aaa", QueryData<string>("select 'aaa'"));
            Assert.Equal('a', QueryData<char>("select 'a'"));
            Assert.Equal(null, QueryData<int?>("select null"));
            Assert.Equal(NormalEnum.B, QueryData<NormalEnum>("select 2"));
            Assert.Equal(NormalEnum.B, QueryData<NormalEnum>("select 'B'"));
            Assert.Throws<Exception>(() => QueryData<NormalEnum>("select convert(varchar, null)"));
            Assert.Equal(null, QueryData<NormalEnum?>("select convert(varchar, null)"));
        }
        #endregion


        #region Model轉Dictionary
        public sealed class DictionaryModel : IDataModel
        {
            public int intCol;

            [Column(NullMapping = "A")]
            public NormalEnum? nullNorEnum;

            [Column(NullMapping = 10)]
            public StringEnum? nullStrEnum;

            [Column(NullMapping = 2D)] //docuble
            public string strCol;

            [Column(NullMapping = 3L)] //long
            public int? nullIntCol;

            [Column(NullMapping = NormalEnum.A)]
            public decimal? nullDecimalCol;

            [Column(NullMapping = 10)]
            public StringEnum?[] nullStrEnumArray;
        }

        [Fact(DisplayName = "Model轉Dictionary")]
        public void Model轉Dictionary()
        {
            var dictFiller = ColumnInfoCollection.GenerateDictionaryFiller(typeof(DictionaryModel));
            var model = new DictionaryModel
            {
                intCol = 30,
                nullDecimalCol = 10,
                nullIntCol = 20,
                nullNorEnum = NormalEnum.B,
                strCol = "a",
                nullStrEnum = StringEnum.C,
                nullStrEnumArray = new StringEnum?[] { StringEnum.A, StringEnum.B, StringEnum.C }
            };
            var dict = new Dictionary<string, object>();
            dictFiller(dict, model);
            Assert.Equal(model.intCol, dict[nameof(DictionaryModel.intCol)]);
            Assert.Equal(model.nullNorEnum, dict[nameof(DictionaryModel.nullNorEnum)]);
            Assert.Equal("cc", dict[nameof(DictionaryModel.nullStrEnum)]);
            Assert.Equal(model.strCol, dict[nameof(DictionaryModel.strCol)]);
            Assert.Equal(model.nullIntCol, dict[nameof(DictionaryModel.nullIntCol)]);
            Assert.Equal(model.nullDecimalCol, dict[nameof(DictionaryModel.nullDecimalCol)]);
            Assert.True(((IEnumerable<object>)dict[nameof(DictionaryModel.nullStrEnumArray)]).SequenceEqual(new object[] { "aa", "bb", "cc" }));

            // null mapping 
            dict.Clear();
            model = new DictionaryModel { nullStrEnumArray = new StringEnum?[] { null, null, null } };
            dictFiller(dict, model);
            Assert.Equal("A", dict[nameof(DictionaryModel.nullNorEnum)]);
            Assert.Equal(10, dict[nameof(DictionaryModel.nullStrEnum)]);
            Assert.Equal(2D, dict[nameof(DictionaryModel.strCol)]);
            Assert.Equal(3L, dict[nameof(DictionaryModel.nullIntCol)]);
            Assert.Equal(NormalEnum.A, dict[nameof(DictionaryModel.nullDecimalCol)]);
            Assert.True(((IEnumerable<object>)dict[nameof(DictionaryModel.nullStrEnumArray)]).SequenceEqual(new object[] { 10, 10, 10 }));
        }
        #endregion
    }
}
