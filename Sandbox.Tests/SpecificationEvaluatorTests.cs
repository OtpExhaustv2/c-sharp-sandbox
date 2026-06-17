using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Specifications;

namespace Sandbox.Tests
{
    [TestClass]
    public class SpecificationEvaluatorTests
    {
        private static List<Widget> Sample() => new()
        {
            new Widget(1, "Alpha", 30m, true),
            new Widget(2, "Bravo", 10m, false),
            new Widget(3, "Charlie", 20m, true),
            new Widget(4, "Delta", 5m, true),
        };

        [TestMethod]
        public void Filters_BySpecCriteria()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new InStockSpec())
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEquivalent(new[] { 1, 3, 4 }, ids);
        }

        [TestMethod]
        public void Orders_Ascending()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new CheapestFirstSpec())
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEqual(new[] { 4, 2, 3, 1 }, ids);
        }

        [TestMethod]
        public void Orders_Descending()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new PriciestFirstSpec())
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEqual(new[] { 1, 3, 2, 4 }, ids);
        }

        [TestMethod]
        public void Pages_WithSkipAndTake()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new PagedByIdSpec(skip: 1, take: 2))
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEqual(new[] { 2, 3 }, ids);
        }

        [TestMethod]
        public void Projects_WithSelector()
        {
            var names = SpecificationEvaluator.Evaluate(Sample(), new WidgetNameSpec()).ToList();

            CollectionAssert.AreEqual(new[] { "Alpha", "Bravo", "Charlie", "Delta" }, names);
        }

        [TestMethod]
        public void Projection_Throws_WhenSelectorIsNull()
        {
            Assert.ThrowsExactly<InvalidOperationException>(
                () => SpecificationEvaluator.Evaluate(Sample(), new NoSelectorSpec()).ToList());
        }

        [TestMethod]
        public void EmptySource_ReturnsEmpty()
        {
            var result = SpecificationEvaluator.Evaluate(new List<Widget>(), new InStockSpec()).ToList();

            Assert.IsEmpty(result);
        }
    }
}
