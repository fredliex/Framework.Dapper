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
                var repository = conn.GetRepository<NoneKeyModel>(new RepositoryOption { Table = tmpTable });
                using (var trace = new DbTraceContext())
                {
                    var oriModel = new NoneKeyModel { decimalCol = 1, intCol = 2, norEnum = NormalEnum.C, strCol = "A", strEnum = StringEnum.C,
                        datetimeCol = DateTime.Now, dateoffsetCol = DateTimeOffset.Now, fakeCol = "abc " };

                    //insert一筆
                    repository.Insert(oriModel);
                    trace.History.Last().Verify(
                        $"insert into {tmpTable} " +
                        $"(norEnum,strEnum,strCol,intCol,decimalCol,datetimeCol,dateoffsetCol,renameCol) values " +
                        $"(@norEnum,@strEnum,@strCol,@intCol,@decimalCol,@datetimeCol,@dateoffsetCol,@realCol)");

                    #region 測試各種select

                    //無查詢條件
                    var model = repository.Select().Single();
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
                    model = repository.Select(oriModel).Single();
                    trace.History.Last().Verify(
                        $"select * from {tmpTable} where " +
                        $"norEnum=@norEnum and strEnum=@strEnum and strCol=@strCol and intCol=@intCol and " +
                        $"decimalCol=@decimalCol and datetimeCol=@datetimeCol and dateoffsetCol=@dateoffsetCol and renameCol=@realCol");

                    //查詢條件為匿名物件
                    var selectAnonymous = new { oriModel.norEnum, oriModel.strEnum, strCol = new[] { oriModel.strCol, "aaa" } };
                    model = repository.Select(selectAnonymous).Single();
                    trace.History.Last().Verify($"select * from {tmpTable} where norEnum=@norEnum and strEnum=@strEnum and strCol in (@strCol1,@strCol2)");

                    //查詢條件為字典
                    var selectDict = new Dictionary<string, object>
                    {
                        [nameof(NoneKeyModel.norEnum)] = oriModel.norEnum,
                        [nameof(NoneKeyModel.strEnum)] = oriModel.strEnum,
                        [nameof(NoneKeyModel.strCol)] = new[] { oriModel.strCol, "aaaa" }
                    };
                    model = repository.Select(selectDict).Single();
                    trace.History.Last().Verify($"select * from {tmpTable} where norEnum=@norEnum and strEnum=@strEnum and strCol in (@strCol1,@strCol2)");

                    #endregion

                    #region 測試update

                    //update條件為model
                    oriModel.strCol = "update-model";
                    Assert.Equal(1, repository.Update(model, oriModel));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        $"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $"datetimeCol=@datetimeCol,dateoffsetCol=@dateoffsetCol,renameCol=@realCol " +
                        $"where " +
                        $"norEnum=@_old_norEnum and strEnum=@_old_strEnum and strCol=@_old_strCol and intCol=@_old_intCol and decimalCol=@_old_decimalCol and " +
                        $"datetimeCol=@_old_datetimeCol and dateoffsetCol=@_old_dateoffsetCol and renameCol=@_old_realCol");
                    model = repository.Select(oriModel).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為匿名物件
                    oriModel.strCol = "update-class";
                    var updateAnonymous = new { model.strCol, strEnum = new[] { model.strEnum, StringEnum.B } };
                    Assert.Equal(1, repository.Update(updateAnonymous, oriModel));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        $"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $"datetimeCol=@datetimeCol,dateoffsetCol=@dateoffsetCol,renameCol=@realCol " +
                        $"where " +
                        $"strCol=@_old_strCol and strEnum in (@_old_strEnum1,@_old_strEnum2)")
                        .Parameters.Verify("_old_strCol", model.strCol)
                        .Verify("_old_strEnum1", "cc")
                        .Verify("_old_strEnum2", "bb");
                    model = repository.Select(oriModel).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為字典
                    oriModel.strCol = "update-dictionary";
                    var updateDict = new Dictionary<string, object>
                    {
                        [nameof(NoneKeyModel.strCol)] = model.strCol,
                        [nameof(NoneKeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(1, repository.Update(updateDict, oriModel));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        $"norEnum=@norEnum,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        $"datetimeCol=@datetimeCol,dateoffsetCol=@dateoffsetCol,renameCol=@realCol " +
                        $"where " +
                        $"strCol=@_old_strCol and strEnum in (@_old_strEnum1,@_old_strEnum2)")
                        .Parameters.Verify("_old_strCol", model.strCol)
                        .Verify("_old_strEnum1", "cc")
                        .Verify("_old_strEnum2", "bb");
                    model = repository.Select(oriModel).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    #endregion 

                    #region 測試delete

                    //delete條件為字典, 這條件理應刪除0筆
                    var deleteDict = new Dictionary<string, object>
                    {
                        [nameof(NoneKeyModel.strCol)] = "not exist",
                        [nameof(NoneKeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(0, repository.Delete(deleteDict));
                    trace.History.Last().Verify($"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為匿名物件, 這條件理應刪除0筆
                    var deleteAnonymous = new { strCol = "not exist", strEnum = new[] { model.strEnum, StringEnum.B } };
                    Assert.Equal(0, repository.Delete(deleteAnonymous));
                    trace.History.Last().Verify(
                        $"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為model, 這條件理應真的會刪除
                    Assert.Equal(1, repository.Delete(model));
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
            public NormalEnum keyCol;
            public StringEnum strEnum;
            public string strCol;
            public int? intCol;
            public decimal decimalCol;
            public DateTime datetimeCol;
            [Column(Behavior = ColumnBehavior.ConcurrencyCheck)]
            public DateTimeOffset concurrencyCol;
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
            using (var conn = OpenConnection())
            {
                var tmpTable = conn.CreateTempTable<KeyModel>();
                var repository = conn.GetRepository<KeyModel>(new RepositoryOption { Table = tmpTable });
                using (var trace = new DbTraceContext())
                {
                    var oriModel = new KeyModel
                    {
                        decimalCol = 1,
                        intCol = 2,
                        keyCol = NormalEnum.C,
                        strCol = "A",
                        strEnum = StringEnum.C,
                        datetimeCol = DateTime.Now,
                        concurrencyCol = DateTimeOffset.Now,
                        fakeCol = "abc "
                    };

                    //insert一筆
                    repository.Insert(oriModel);
                    trace.History.Last().Verify($"insert into {tmpTable} " +
                        "(keyCol,strEnum,strCol,intCol,decimalCol,datetimeCol,concurrencyCol,renameCol) values " +
                        "(@keyCol,@strEnum,@strCol,@intCol,@decimalCol,@datetimeCol,sysdatetimeoffset(),@realCol)");

                    #region 測試各種select

                    //無查詢條件
                    var model = repository.Select().Single();
                    trace.History.Last().Verify($"select * from {tmpTable}");
                    Assert.Equal(oriModel.decimalCol, model.decimalCol);
                    Assert.Equal(oriModel.intCol, model.intCol);
                    Assert.Equal(oriModel.keyCol, model.keyCol);
                    Assert.Equal(oriModel.strCol, model.strCol);
                    Assert.Equal(oriModel.strEnum, model.strEnum);
                    AssertSqlDatetimeEqual(oriModel.datetimeCol, model.datetimeCol);
                    Assert.NotEqual(model.concurrencyCol, oriModel.concurrencyCol);
                    Assert.Equal(oriModel.fakeCol.TrimEnd(), model.fakeCol);

                    //查詢條件為model
                    model = repository.Select(model).Single();
                    trace.History.Last().Verify($"select * from {tmpTable} where keyCol=@keyCol and concurrencyCol=@concurrencyCol");
                    #endregion

                    #region 測試update

                    //update條件為model
                    var newStrCol = "update-model";
                    model.strCol = newStrCol;
                    Assert.Equal(1, repository.Update(model, model));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        "strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        "datetimeCol=@datetimeCol,concurrencyCol=sysdatetimeoffset(),renameCol=@realCol " +
                        "where keyCol=@keyCol and concurrencyCol=@concurrencyCol");
                    model = repository.Select(new { model.keyCol }).Single();
                    Assert.Equal(newStrCol, model.strCol);

                    //update條件為匿名物件
                    oriModel = model;
                    var updateAnonymous = new { oriModel.strCol, strEnum = new[] { oriModel.strEnum, StringEnum.B } };
                    oriModel.strCol = "update-class";
                    Assert.Equal(1, repository.Update(updateAnonymous, oriModel));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        "keyCol=@keyCol,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        "datetimeCol=@datetimeCol,concurrencyCol=sysdatetimeoffset(),renameCol=@realCol " +
                        "where strCol=@_old_strCol and strEnum in (@_old_strEnum1,@_old_strEnum2)");
                    trace.History.Last().Parameters
                        .Verify("_old_strCol", updateAnonymous.strCol)
                        .Verify("_old_strEnum1", "cc")
                        .Verify("_old_strEnum2", "bb");
                    model = repository.Select(new { model.keyCol }).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    //update條件為字典
                    oriModel.strCol = "update-dictionary";
                    var updateDict = new Dictionary<string, object>
                    {
                        [nameof(KeyModel.strCol)] = model.strCol,
                        [nameof(KeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(1, repository.Update(updateDict, oriModel));
                    trace.History.Last().Verify(
                        $"update {tmpTable} set " +
                        "keyCol=@keyCol,strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol," +
                        "datetimeCol=@datetimeCol,concurrencyCol=sysdatetimeoffset(),renameCol=@realCol " +
                        "where strCol=@_old_strCol and strEnum in (@_old_strEnum1,@_old_strEnum2)");
                    trace.History.Last().Parameters
                        .Verify("_old_strCol", updateDict[nameof(KeyModel.strCol)])
                        .Verify("_old_strEnum1", "cc")
                        .Verify("_old_strEnum2", "bb");
                    model = repository.Select(new { model.keyCol }).Single();
                    Assert.Equal(oriModel.strCol, model.strCol);

                    #endregion 

                    #region 測試delete

                    //delete條件為字典, 這條件理應刪除0筆
                    var deleteDict = new Dictionary<string, object>
                    {
                        [nameof(KeyModel.strCol)] = "not exist",
                        [nameof(KeyModel.strEnum)] = new[] { model.strEnum, StringEnum.B }
                    };
                    Assert.Equal(0, repository.Delete(deleteDict));
                    trace.History.Last().Verify($"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為匿名物件, 這條件理應刪除0筆
                    var deleteAnonymous = new { strCol = "not exist", strEnum = new[] { model.strEnum, StringEnum.B } };
                    Assert.Equal(0, repository.Delete(deleteAnonymous));
                    trace.History.Last().Verify(
                        $"delete from {tmpTable} where strCol=@strCol and strEnum in (@strEnum1,@strEnum2)")
                        .Parameters.Verify("strCol", "not exist")
                        .Verify("strEnum1", "cc")
                        .Verify("strEnum2", "bb");

                    //delete條件為model, 這條件理應真的會刪除
                    Assert.Equal(1, repository.Delete(model));
                    trace.History.Last().Verify($"delete from {tmpTable} where keyCol=@keyCol and concurrencyCol=@concurrencyCol");

                    #endregion 
                }
                conn.Execute($"drop table {tmpTable}");
            }
        }


        [Fact(DisplayName = "model集合")]
        public void TestModels()
        {
            using (var conn = OpenConnection())
            {
                var tmpTable = conn.CreateTempTable<KeyModel>();
                var repository = conn.GetRepository<KeyModel>(new RepositoryOption { Table = tmpTable });
                using (var trace = new DbTraceContext())
                {
                    //insert 
                    var oriModels = new[]
                    {
                        new KeyModel { keyCol = NormalEnum.A, datetimeCol = DateTime.Now },
                        new KeyModel { keyCol = NormalEnum.B, datetimeCol = DateTime.Now },
                        new KeyModel { keyCol = NormalEnum.C, datetimeCol = DateTime.Now }
                    };
                    Assert.Equal(oriModels.Length, repository.Inserts(oriModels));
                    Assert.Equal(oriModels.Length, trace.History.Count);
                    trace.History.ForEach(n => n.Verify(
                        $"insert into {tmpTable} (keyCol,strEnum,strCol,intCol,decimalCol,datetimeCol,concurrencyCol,renameCol)" +
                        $" values (@keyCol,@strEnum,@strCol,@intCol,@decimalCol,@datetimeCol,sysdatetimeoffset(),@realCol)"));
                    var models = repository.Select().ToList();

                    //update
                    trace.History.Clear();
                    models.ForEach(n => n.decimalCol = 10);
                    Assert.Equal(oriModels.Length, repository.Updates(models));
                    trace.History.ForEach(n => n.Verify(
                        $"update {tmpTable} set strEnum=@strEnum,strCol=@strCol,intCol=@intCol,decimalCol=@decimalCol,datetimeCol=@datetimeCol,concurrencyCol=sysdatetimeoffset(),renameCol=@realCol" +
                        $" where keyCol=@keyCol and concurrencyCol=@concurrencyCol"));
                    models = repository.Select().ToList();
                    Assert.True(models.All(n => n.decimalCol == 10));

                    //select 
                    trace.History.Clear();
                    Assert.Equal(2, repository.Select(models.Take(2)).Count());
                    trace.History.ForEach(n => n.Verify($"select * from {tmpTable} where keyCol=@keyCol and concurrencyCol=@concurrencyCol"));

                    //delete
                    trace.History.Clear();
                    Assert.Equal(oriModels.Length, repository.Deletes(models));
                    trace.History.ForEach(n => n.Verify(
                        $"delete from {tmpTable} where keyCol=@keyCol and concurrencyCol=@concurrencyCol"));
                    models = repository.Select().ToList();
                    Assert.Empty(models);
                }

                conn.Execute($"drop table {tmpTable}");
            }
        }

    }
}
