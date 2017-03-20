using Framework.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using static Framework.Test.Helper;

namespace Framework.Test
{
    public class RepositoryTest
    {
        public enum NormalEnum { A = 1, B = 2, C = 3 }
        public enum StringEnum
        {
            [DbValue("aa")]A,
            [DbValue("bb")]B,
            [DbValue("cc")]C
        }

        public sealed class NoneKeyModel : IDataModel
        {
            public NormalEnum norEnum;
            public StringEnum strEnum;
            public string strCol;
            public int? intCol;
            public decimal decimalCol;
            public DateTime datetimeCol;
            public DateTimeOffset dateoffsetCol;
            [NonColumn]
            public string fakeCol {
                get { return realCol; }
                set { realCol = value; }
            }
            [Column("renameCol", Behavior = ColumnBehavior.TrimRight)]
            private string realCol;
        }

        [Fact(DisplayName = "無定義Key")]
        public void TestNoneKeyModel()
        {
            using (var conn = OpenConnection())
            {
                var tmpTable = conn.CreateTempTable<NoneKeyModel>();
                var repOpt = new RepositoryOption { Table = tmpTable };
                using (var trace = new DbTraceContext())
                {
                    var oriModel = new NoneKeyModel { decimalCol = 1, intCol = 2, norEnum = NormalEnum.C, strCol = "A", strEnum = StringEnum.C,
                        datetimeCol = DateTime.Now, dateoffsetCol = DateTimeOffset.Now, fakeCol = "abc " };

                    //insert一筆
                    oriModel.Insert(conn, repOpt);
                    trace.History.Last().Verify(
                        $"insert into {tmpTable} " +
                        $"(norEnum,strEnum,strCol,intCol,decimalCol,datetimeCol,dateoffsetCol,renameCol) values " +
                        $"(@norEnum,@strEnum,@strCol,@intCol,@decimalCol,@datetimeCol,@dateoffsetCol,@realCol)");

                    #region 測試各種select

                    //無查詢條件
                    var model = Repository.Select<NoneKeyModel>(conn, repOpt).Single();
                    trace.History.Last().Verify($"select * from {tmpTable}");
                    Assert.Equal(oriModel.decimalCol, model.decimalCol);
                    Assert.Equal(oriModel.intCol, model.intCol);
                    Assert.Equal(oriModel.norEnum, model.norEnum);
                    Assert.Equal(oriModel.strCol, model.strCol);
                    Assert.Equal(oriModel.strEnum, model.strEnum);
                    AssertSqlDatetimeEqual(oriModel.datetimeCol, model.datetimeCol);  
                    Assert.Equal(oriModel.dateoffsetCol, model.dateoffsetCol);
                    Assert.Equal(oriModel.fakeCol.TrimEnd(), model.fakeCol);

                    //查詢條件為model
                    model = Repository.Select<NoneKeyModel>(conn, oriModel, repOpt).Single();
                    trace.History.Last().Verify(
                        $"select * from {tmpTable} where " +
                        $"norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and " +
                        $"decimalCol=@decimalCol and datetimeCol=@datetimeCol and dateoffsetCol=@dateoffsetCol and renameCol=@realCol");

                    //查詢條件為匿名物件
                    var selectAnonymous = new { oriModel.norEnum, oriModel.strEnum, strCol = new[] { oriModel.strCol, "aaa" } };
                    model = Repository.Select<NoneKeyModel>(conn, selectAnonymous, repOpt).Single();
                    trace.History.Last().Verify($"select * from {tmpTable} where norEnum=@norEnum and strEnum=@strEnum and strCol in (@strCol1,@strCol2)");

                    //查詢條件為字典
                    var selectDict = new Dictionary<string, object>
                    {
                        [nameof(NoneKeyModel.norEnum)] = oriModel.norEnum,
                        [nameof(NoneKeyModel.strEnum)] = oriModel.strEnum,
                        [nameof(NoneKeyModel.strCol)] = new[] { oriModel.strCol, "aaaa" }
                    };
                    model = Repository.Select<NoneKeyModel>(conn, selectDict, repOpt).Single();
                    trace.History.Last().Verify($"select * from {tmpTable} where norEnum=@norEnum and strEnum=@strEnum and strCol in (@strCol1,@strCol2)");

                    #endregion

                    #region 測試update

                    //update條件為model
                    oriModel.strCol = "update-model";
                    Assert.Equal(1, oriModel.Update(conn, model, repOpt));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        $"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $"datetimeCol=@datetimeCol,dateoffsetCol=@dateoffsetCol,renameCol=@realCol " +
                        $"where " +
                        $"norEnum=@_key_norEnum and strEnum=@_key_strEnum and strCol=@_key_strCol and intCol=@_key_intCol and decimalCol=@_key_decimalCol and " +
                        $"datetimeCol=@_key_datetimeCol and dateoffsetCol=@_key_dateoffsetCol and renameCol=@_key_realCol");
                    model = Repository.Select<NoneKeyModel>(conn, oriModel, repOpt).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為匿名物件
                    oriModel.strCol = "update-class";
                    var updateAnonymous = new { model.strCol, strEnum = new[] { model.strEnum, StringEnum.B } };
                    Assert.Equal(1, oriModel.Update(conn, updateAnonymous, repOpt));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        $"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $"datetimeCol=@datetimeCol,dateoffsetCol=@dateoffsetCol,renameCol=@realCol " +
                        $"where " +
                        $"strCol=@_key_strCol and strEnum in (@_key_strEnum1,@_key_strEnum2)")
                        .Parameters.Verify("_key_strCol", model.strCol)
                        .Verify("_key_strEnum1", "cc")
                        .Verify("_key_strEnum2", "bb");
                    model = Repository.Select<NoneKeyModel>(conn, oriModel, repOpt).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為字典
                    oriModel.strCol = "update-dictionary";
                    var updateDict = new Dictionary<string, object>
                    {
                        [nameof(NoneKeyModel.strCol)] = model.strCol,
                        [nameof(NoneKeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(1, oriModel.Update(conn, updateDict,repOpt));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        $"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $"datetimeCol=@datetimeCol,dateoffsetCol=@dateoffsetCol,renameCol=@realCol " +
                        $"where " +
                        $"strCol=@_key_strCol and strEnum in (@_key_strEnum1,@_key_strEnum2)")
                        .Parameters.Verify("_key_strCol", model.strCol)
                        .Verify("_key_strEnum1", "cc")
                        .Verify("_key_strEnum2", "bb");
                    model = Repository.Select<NoneKeyModel>(conn, oriModel, repOpt).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    #endregion 

                    #region 測試delete

                    //delete條件為字典, 這條件理應刪除0筆
                    var deleteDict = new Dictionary<string, object>
                    {
                        [nameof(NoneKeyModel.strCol)] = "not exist",
                        [nameof(NoneKeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(0, Repository.Delete<NoneKeyModel>(conn, deleteDict,repOpt));
                    trace.History.Last().Verify($"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為匿名物件, 這條件理應刪除0筆
                    var deleteAnonymous = new { strCol = "not exist", strEnum = new[] { model.strEnum, StringEnum.B } };
                    Assert.Equal(0, Repository.Delete<NoneKeyModel>(conn, deleteAnonymous, repOpt));
                    trace.History.Last().Verify(
                        $"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為model, 這條件理應真的會刪除
                    Assert.Equal(1, model.Delete(conn, repOpt));
                    trace.History.Last().Verify(
                        $"delete from {tmpTable} where " +
                        $"norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and decimalCol=@decimalCol and " +
                        $"datetimeCol=@datetimeCol and dateoffsetCol=@dateoffsetCol and renameCol=@realCol");

                    #endregion 
                }
                conn.Execute($"drop table {tmpTable}");
            }
        }


        public sealed class KeyModel : IDataModel
        {
            [Column(Behavior = ColumnBehavior.Key)]
            public NormalEnum norEnum;
            public StringEnum strEnum;
            public string strCol;
            public int? intCol;
            public decimal decimalCol;
            public DateTime datetimeCol;
            [Column(Behavior = ColumnBehavior.ConcurrencyCheck)]
            public DateTimeOffset dateoffsetCol;
            [NonColumn]
            public string fakeCol
            {
                get { return realCol; }
                set { realCol = value; }
            }
            [Column("renameCol", Behavior = ColumnBehavior.TrimRight)]
            private string realCol;
        }

        [Fact(DisplayName = "有定義Key")]
        public void TestKeyModel()
        {
            var regPatternDateoffset = "\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}.\\d{5}\\+\\d{2}:\\d{2}";
            using (var conn = OpenConnection())
            {
                var tmpTable = conn.CreateTempTable<KeyModel>();
                var repOpt = new RepositoryOption { Table = tmpTable };
                using (var trace = new DbTraceContext())
                {
                    var oriModel = new KeyModel
                    {
                        decimalCol = 1,
                        intCol = 2,
                        norEnum = NormalEnum.C,
                        strCol = "A",
                        strEnum = StringEnum.C,
                        datetimeCol = DateTime.Now,
                        dateoffsetCol = DateTimeOffset.Now,
                        fakeCol = "abc "
                    };

                    //insert一筆
                    oriModel.Insert(conn, repOpt);
                    Assert.Matches(
                        $@"insert into {tmpTable} " +
                        $@"\(norEnum,strEnum,strCol,intCol,decimalCol,datetimeCol,dateoffsetCol,renameCol\) values " +
                        $@"\(@norEnum,@strEnum,@strCol,@intCol,@decimalCol,@datetimeCol,'{regPatternDateoffset}',@realCol\)",
                        trace.History.Last().CommandText);

                    #region 測試各種select

                    //無查詢條件
                    var model = Repository.Select<KeyModel>(conn, repOpt).Single();
                    trace.History.Last().Verify($"select * from {tmpTable}");
                    Assert.Equal(oriModel.decimalCol, model.decimalCol);
                    Assert.Equal(oriModel.intCol, model.intCol);
                    Assert.Equal(oriModel.norEnum, model.norEnum);
                    Assert.Equal(oriModel.strCol, model.strCol);
                    Assert.Equal(oriModel.strEnum, model.strEnum);
                    AssertSqlDatetimeEqual(oriModel.datetimeCol, model.datetimeCol);
                    Assert.True(model.dateoffsetCol > oriModel.dateoffsetCol);
                    Assert.Equal(oriModel.fakeCol.TrimEnd(), model.fakeCol);

                    //查詢條件為model
                    model = Repository.Select(conn, oriModel, repOpt).Single();
                    trace.History.Last().Verify($"select * from {tmpTable} where norEnum=@norEnum");
                    #endregion

                    #region 測試update

                    //update條件為model
                    oriModel = model;
                    oriModel.strCol = "update-model";
                    Assert.Equal(1, oriModel.Update(conn, repOpt));
                    Assert.Matches(
                        $@"update {tmpTable} set " +
                        $@"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $@"datetimeCol=@datetimeCol,dateoffsetCol='{regPatternDateoffset}',renameCol=@realCol " +
                        $@"where " +
                        $@"norEnum=@_key_norEnum and dateoffsetCol=@_key_dateoffsetCol",
                        trace.History.Last().CommandText);
                    model = Repository.Select<KeyModel>(conn, oriModel, repOpt).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為匿名物件
                    oriModel = model;
                    var updateAnonymous = new { oriModel.strCol, strEnum = new[] { oriModel.strEnum, StringEnum.B } };
                    oriModel.strCol = "update-class";
                    Assert.Equal(1, oriModel.Update(conn, updateAnonymous, repOpt));
                    Assert.Matches(
                        $@"update {tmpTable} set " +
                        $@"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $@"datetimeCol=@datetimeCol,dateoffsetCol='{regPatternDateoffset}',renameCol=@realCol " +
                        $@"where " +
                        $@"strCol=@_key_strCol and strEnum in \(@_key_strEnum1,@_key_strEnum2\)",
                        trace.History.Last().CommandText);
                    trace.History.Last().Parameters
                        .Verify("_key_strCol", updateAnonymous.strCol)
                        .Verify("_key_strEnum1", "cc")
                        .Verify("_key_strEnum2", "bb");
                    model = Repository.Select<KeyModel>(conn, oriModel, repOpt).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為字典
                    oriModel.strCol = "update-dictionary";
                    var updateDict = new Dictionary<string, object>
                    {
                        [nameof(KeyModel.strCol)] = model.strCol,
                        [nameof(KeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(1, oriModel.Update(conn, updateDict, repOpt));
                    var aa = $@"update {tmpTable} set " +
                        $@"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $@"datetimeCol=@datetimeCol,dateoffsetCol='{regPatternDateoffset}',renameCol=@realCol " +
                        $@"where " +
                        $@"strCol=@_key_strCol and strEnum in \(@_key_strEnum1,@_key_strEnum2\)";
                    Assert.Matches(
                        $@"update {tmpTable} set " +
                        $@"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $@"datetimeCol=@datetimeCol,dateoffsetCol='{regPatternDateoffset}',renameCol=@realCol " +
                        $@"where " +
                        $@"strCol=@_key_strCol and strEnum in \(@_key_strEnum1,@_key_strEnum2\)",
                        trace.History.Last().CommandText);
                    trace.History.Last().Parameters
                        .Verify("_key_strCol", updateDict[nameof(KeyModel.strCol)])
                        .Verify("_key_strEnum1", "cc")
                        .Verify("_key_strEnum2", "bb");
                    model = Repository.Select<KeyModel>(conn, oriModel, repOpt).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    #endregion 

                    #region 測試delete

                    //delete條件為字典, 這條件理應刪除0筆
                    var deleteDict = new Dictionary<string, object>
                    {
                        [nameof(KeyModel.strCol)] = "not exist",
                        [nameof(KeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(0, Repository.Delete<KeyModel>(conn, deleteDict, repOpt));
                    trace.History.Last().Verify($"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為匿名物件, 這條件理應刪除0筆
                    var deleteAnonymous = new { strCol = "not exist", strEnum = new[] { model.strEnum, StringEnum.B } };
                    Assert.Equal(0, Repository.Delete<KeyModel>(conn, deleteAnonymous, repOpt));
                    trace.History.Last().Verify(
                        $"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為model, 這條件理應真的會刪除
                    Assert.Equal(1, model.Delete(conn, repOpt));
                    trace.History.Last().Verify($"delete from {tmpTable} where norEnum=@norEnum and dateoffsetCol=@dateoffsetCol");

                    #endregion 
                }
                conn.Execute($"drop table {tmpTable}");
            }
        }
    }
}
