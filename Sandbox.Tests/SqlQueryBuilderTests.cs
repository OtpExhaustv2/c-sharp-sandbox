using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Sql;

namespace Sandbox.Tests
{
    [TestClass]
    public class SqlQueryBuilderTests
    {
        [TestMethod]
        public void BareTable_SelectsStar()
        {
            var compiled = SqlQuery.From("Products").ToSql();

            Assert.AreEqual("SELECT * FROM [Products]", compiled.Sql);
            Assert.AreEqual(0, compiled.Parameters.Count);
        }
        [TestMethod]
        public void Where_Comparison_EmitsColumnOpParam()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_ValueOnLeft_FlipsOperator()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => 50m < (decimal)r["Price"])
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_CapturedLocal_IsFuncletizedToParam()
        {
            var min = 50m;
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > min)
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_AndOrNot_AreParenthesized()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => ((decimal)r["Price"] > 50m && (string)r["Category"] == "Tools")
                            || !((bool)r["IsAvailable"] == true))
                .ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] WHERE (([Price] > @p0 AND [Category] = @p1) OR NOT ([IsAvailable] = @p2))",
                compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
            Assert.AreEqual("Tools", compiled.Parameters["@p1"]);
            Assert.AreEqual(true, compiled.Parameters["@p2"]);
        }

        [TestMethod]
        public void Where_StringMethods_TranslateToLike()
        {
            var contains = SqlQuery.From("Products").Where(r => ((string)r["Name"]).Contains("lap")).ToSql();
            Assert.AreEqual("SELECT * FROM [Products] WHERE [Name] LIKE @p0", contains.Sql);
            Assert.AreEqual("%lap%", contains.Parameters["@p0"]);

            var starts = SqlQuery.From("Products").Where(r => ((string)r["Name"]).StartsWith("lap")).ToSql();
            Assert.AreEqual("lap%", starts.Parameters["@p0"]);

            var ends = SqlQuery.From("Products").Where(r => ((string)r["Name"]).EndsWith("top")).ToSql();
            Assert.AreEqual("%top", ends.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_NullChecks_TranslateToIsNull()
        {
            var isNull = SqlQuery.From("Orders").Where(r => r["CompletedAt"] == null).ToSql();
            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NULL", isNull.Sql);
            Assert.AreEqual(0, isNull.Parameters.Count);

            var isNotNull = SqlQuery.From("Orders").Where(r => r["CompletedAt"] != null).ToSql();
            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NOT NULL", isNotNull.Sql);
        }
    }
}
