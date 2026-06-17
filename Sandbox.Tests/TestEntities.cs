using Sandbox.Core.Specifications;

namespace Sandbox.Tests
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
}
