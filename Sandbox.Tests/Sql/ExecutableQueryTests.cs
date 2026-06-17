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
    }
}
