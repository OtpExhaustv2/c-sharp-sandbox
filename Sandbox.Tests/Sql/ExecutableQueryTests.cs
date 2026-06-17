using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Sql;

namespace Sandbox.Tests.Sql
{
    [TestClass]
    public class ExecutableQueryTests
    {
        [TestMethod]
        public async Task ToListAsync_OnUnboundQuery_Throws()
        {
            // A query from SqlQuery.From is not bound to a connection.
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => SqlQuery.From("Products").ToListAsync());
        }

        [TestMethod]
        public async Task CountAsync_OnUnboundQuery_Throws()
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => SqlQuery.From("Products").CountAsync());
        }

        [TestMethod]
        public void ToCountSql_BareTable()
        {
            var compiled = SqlQuery.From("Products").ToCountSql();

            Assert.AreEqual("SELECT COUNT(*) FROM [Products]", compiled.Sql);
            Assert.IsEmpty(compiled.Parameters);
        }

        [TestMethod]
        public void ToCountSql_WithWhere()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .ToCountSql();

            Assert.AreEqual("SELECT COUNT(*) FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void ToCountSql_WithPaging_Throws()
        {
            Assert.ThrowsExactly<NotSupportedException>(
                () => SqlQuery.From("Products").Skip(10).Take(5).ToCountSql());
        }
    }
}
