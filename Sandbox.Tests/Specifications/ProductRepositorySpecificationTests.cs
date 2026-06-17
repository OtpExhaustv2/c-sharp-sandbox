using Microsoft.VisualStudio.TestTools.UnitTesting;
using sandbox_api.Data;
using sandbox_api.Repositories;
using sandbox_api.Specifications;

namespace Sandbox.Tests.Specifications
{
    [TestClass]
    public class ProductRepositorySpecificationTests
    {
        [TestMethod]
        public async Task GetBySpecificationAsync_AppliesSpec_AgainstSimulatedDatabase()
        {
            var repo = new ProductRepository(new SimulatedDatabase());

            var result = await repo.GetBySpecificationAsync(new AvailableProductSpec());

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotEmpty(result.Value);
            Assert.IsTrue(result.Value.All(p => p.IsAvailable && p.StockQuantity > 0));
        }
    }
}
