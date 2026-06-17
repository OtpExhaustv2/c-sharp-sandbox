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
            Assert.IsEmpty(compiled.Parameters);
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
        public void Where_NotEqualAndGreaterEqual_Translate()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (string)r["Category"] != "Toys" && (int)r["Stock"] >= 5)
                .ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] WHERE ([Category] <> @p0 AND [Stock] >= @p1)",
                compiled.Sql);
            Assert.AreEqual("Toys", compiled.Parameters["@p0"]);
            Assert.AreEqual(5, compiled.Parameters["@p1"]);
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
            Assert.IsTrue((bool)compiled.Parameters["@p2"]!);
        }

        [TestMethod]
        public void MultipleWhere_AreAndCombinedAndParenthesized()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .Where(r => (string)r["Category"] == "Tools")
                .ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] WHERE ([Price] > @p0) AND ([Category] = @p1)",
                compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
            Assert.AreEqual("Tools", compiled.Parameters["@p1"]);
        }

        [TestMethod]
        public void Where_StringMethods_TranslateToLike()
        {
            var contains = SqlQuery.From("Products").Where(r => ((string)r["Name"]).Contains("lap")).ToSql();
            Assert.AreEqual("SELECT * FROM [Products] WHERE [Name] LIKE @p0", contains.Sql);
            Assert.AreEqual("%lap%", contains.Parameters["@p0"]);

            var starts = SqlQuery.From("Products").Where(r => ((string)r["Name"]).StartsWith("lap")).ToSql();
            Assert.AreEqual("SELECT * FROM [Products] WHERE [Name] LIKE @p0", starts.Sql);
            Assert.AreEqual("lap%", starts.Parameters["@p0"]);

            var ends = SqlQuery.From("Products").Where(r => ((string)r["Name"]).EndsWith("top")).ToSql();
            Assert.AreEqual("SELECT * FROM [Products] WHERE [Name] LIKE @p0", ends.Sql);
            Assert.AreEqual("%top", ends.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_Contains_EscapesLikeWildcards()
        {
            var compiled = SqlQuery.From("Coupons")
                .Where(r => ((string)r["Code"]).Contains("50%_x"))
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Coupons] WHERE [Code] LIKE @p0", compiled.Sql);
            Assert.AreEqual("%50[%][_]x%", compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_NullChecks_TranslateToIsNull()
        {
            var isNull = SqlQuery.From("Orders").Where(r => r["CompletedAt"] == null).ToSql();
            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NULL", isNull.Sql);
            Assert.IsEmpty(isNull.Parameters);

            var isNotNull = SqlQuery.From("Orders").Where(r => r["CompletedAt"] != null).ToSql();
            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NOT NULL", isNotNull.Sql);
        }

        [TestMethod]
        public void Where_CastWrappedNull_TranslatesToIsNull()
        {
            // `(string)null` is converted to object for the comparison, wrapping the null
            // constant in a Convert node — IsNull must see through it.
            var compiled = SqlQuery.From("Orders")
                .Where(r => r["CompletedAt"] == (string)null!)
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NULL", compiled.Sql);
            Assert.IsEmpty(compiled.Parameters);
        }
    }
}
