using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Results;

namespace Sandbox.Tests.Results
{
    [TestClass]
    public class ResultSmokeTests
    {
        [TestMethod]
        public void Map_TransformsSuccessValue()
        {
            var result = Result<int, string>.Success(2).Map(x => x * 10);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(20, result.Value);
        }

        [TestMethod]
        public void Combine_ReturnsFirstError()
        {
            var combined = ResultHelpers.Combine(
                Result<int, string>.Success(1),
                Result<int, string>.Failure("boom"));

            Assert.IsTrue(combined.IsFailure);
            Assert.AreEqual("boom", combined.Error);
        }
    }
}
