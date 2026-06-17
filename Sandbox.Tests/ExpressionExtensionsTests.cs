using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Specifications;

namespace Sandbox.Tests
{
    [TestClass]
    public class ExpressionExtensionsTests
    {
        [TestMethod]
        public void And_IsTrueOnlyWhenBothTrue()
        {
            Expression<Func<int, bool>> gt2 = x => x > 2;
            Expression<Func<int, bool>> lt10 = x => x < 10;

            var predicate = gt2.And(lt10).Compile();

            Assert.IsTrue(predicate(5));
            Assert.IsFalse(predicate(1));
            Assert.IsFalse(predicate(20));
        }

        [TestMethod]
        public void Or_IsTrueWhenEitherTrue()
        {
            Expression<Func<int, bool>> lt0 = x => x < 0;
            Expression<Func<int, bool>> gt100 = x => x > 100;

            var predicate = lt0.Or(gt100).Compile();

            Assert.IsTrue(predicate(-5));
            Assert.IsTrue(predicate(200));
            Assert.IsFalse(predicate(50));
        }

        [TestMethod]
        public void Not_InvertsThePredicate()
        {
            Expression<Func<int, bool>> isEven = x => x % 2 == 0;

            var predicate = isEven.Not().Compile();

            Assert.IsFalse(predicate(4));
            Assert.IsTrue(predicate(3));
        }

        [TestMethod]
        public void And_PreservesNestedLambdaParameters()
        {
            // The left predicate contains an inner lambda (`n => n == x`). Rebinding must
            // only touch the OUTER parameter `x`, never the inner `n`. A naive replacer that
            // rewrites every ParameterExpression turns this into `Any(n => n == n)` (always
            // true) and the AND silently loses the set-membership constraint.
            Expression<Func<int, bool>> inSet = x => new[] { 1, 2, 3 }.Any(n => n == x);
            Expression<Func<int, bool>> positive = x => x > 0;

            var predicate = inSet.And(positive).Compile();

            Assert.IsTrue(predicate(2));
            Assert.IsFalse(predicate(5));
        }
    }
}
