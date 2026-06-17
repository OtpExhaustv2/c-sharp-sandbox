using Sandbox.Core.Specifications;

namespace Sandbox.Tests.Specifications
{
    public record Widget(int Id, string Name, decimal Price, bool InStock);

    internal sealed class InStockSpec : Specification<Widget>
    {
        public InStockSpec() : base(w => w.InStock) { }
    }

    internal sealed class PriceBelowSpec : Specification<Widget>
    {
        public PriceBelowSpec(decimal max) : base(w => w.Price < max) { }
    }

    internal sealed class CheapestFirstSpec : Specification<Widget>
    {
        public CheapestFirstSpec() => AddOrderBy(w => w.Price);
    }

    internal sealed class PriciestFirstSpec : Specification<Widget>
    {
        public PriciestFirstSpec() => AddOrderByDescending(w => w.Price);
    }

    internal sealed class PagedByIdSpec : Specification<Widget>
    {
        public PagedByIdSpec(int skip, int take)
        {
            AddOrderBy(w => w.Id);
            ApplyPaging(skip, take);
        }
    }

    internal sealed class WidgetNameSpec : Specification<Widget, string>
    {
        public WidgetNameSpec() => AddSelector(w => w.Name);
    }

    internal sealed class NoSelectorSpec : Specification<Widget, string>
    {
        // Intentionally does not call AddSelector — used to test the evaluator guard.
    }
}
