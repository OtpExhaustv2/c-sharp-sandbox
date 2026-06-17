using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sandbox.Tests
{
    [TestClass]
    public class SpecificationTests
    {
        [TestMethod]
        public void IsSatisfiedBy_ReflectsCriteria()
        {
            var spec = new InStockSpec();

            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(1, "A", 5m, true)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(2, "B", 5m, false)));
        }

        [TestMethod]
        public void And_RequiresBothCriteria()
        {
            var spec = new InStockSpec().And(new PriceBelowSpec(10m));

            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(1, "A", 5m, true)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(2, "B", 50m, true)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(3, "C", 5m, false)));
        }

        [TestMethod]
        public void Or_RequiresEitherCriteria()
        {
            var spec = new InStockSpec().Or(new PriceBelowSpec(10m));

            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(1, "A", 50m, true)));
            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(2, "B", 5m, false)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(3, "C", 50m, false)));
        }

        [TestMethod]
        public void Not_InvertsCriteria()
        {
            var spec = new InStockSpec().Not();

            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(1, "A", 5m, true)));
            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(2, "B", 5m, false)));
        }
    }
}
