using Sandbox.Core.Specifications;
using sandbox_api.Models;

namespace sandbox_api.Specifications
{
    public sealed class AvailableProductSpec : Specification<Product>
    {
        public AvailableProductSpec() : base(p => p.IsAvailable && p.StockQuantity > 0) { }
    }

    public sealed class PriceBelowSpec : Specification<Product>
    {
        public PriceBelowSpec(decimal max) : base(p => p.Price < max) { }
    }

    public sealed class InCategorySpec : Specification<Product>
    {
        public InCategorySpec(string category) : base(p => p.Category == category) { }
    }
}
