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
    }
}
