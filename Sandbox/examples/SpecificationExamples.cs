using Sandbox.Core.Specifications;

namespace Sandbox.examples
{
    public class SpecificationExamples
    {
        public record Gadget(int Id, string Name, decimal Price, string Category, bool InStock);

        private sealed class InStockSpec : Specification<Gadget>
        {
            public InStockSpec() : base(g => g.InStock) { }
        }

        private sealed class PriceBelowSpec : Specification<Gadget>
        {
            public PriceBelowSpec(decimal max) : base(g => g.Price < max) { }
        }

        private sealed class CheapInStockSpec : Specification<Gadget>
        {
            public CheapInStockSpec(decimal max)
            {
                AddCriteria(g => g.InStock && g.Price < max);
                AddOrderBy(g => g.Price);
                ApplyPaging(skip: 0, take: 3);
            }
        }

        private sealed class GadgetNameSpec : Specification<Gadget, string>
        {
            public GadgetNameSpec() => AddSelector(g => g.Name);
        }

        public static void Main()
        {
            var gadgets = new List<Gadget>
            {
                new(1, "Widget", 30m, "Tools", true),
                new(2, "Sprocket", 10m, "Tools", false),
                new(3, "Cog", 20m, "Tools", true),
                new(4, "Bolt", 5m, "Hardware", true),
                new(5, "Nut", 2m, "Hardware", true),
            };

            Console.WriteLine("=== IsSatisfiedBy ===");
            var inStock = new InStockSpec();
            Console.WriteLine($"Sprocket in stock? {inStock.IsSatisfiedBy(gadgets[1])}");

            Console.WriteLine("\n=== And: in stock AND under 15 ===");
            var cheapAndAvailable = new InStockSpec().And(new PriceBelowSpec(15m));
            foreach (var g in SpecificationEvaluator.Evaluate(gadgets, cheapAndAvailable))
                Console.WriteLine($"  {g.Name} ({g.Price:C})");

            Console.WriteLine("\n=== Or / Not ===");
            var pricyOrOutOfStock = new PriceBelowSpec(15m).Not().Or(new InStockSpec().Not());
            foreach (var g in SpecificationEvaluator.Evaluate(gadgets, pricyOrOutOfStock))
                Console.WriteLine($"  {g.Name} ({g.Price:C}, inStock={g.InStock})");

            Console.WriteLine("\n=== Ordered + paged (cheapest 3 in-stock under 25) ===");
            foreach (var g in SpecificationEvaluator.Evaluate(gadgets, new CheapInStockSpec(25m)))
                Console.WriteLine($"  {g.Name} ({g.Price:C})");

            Console.WriteLine("\n=== Projection (names only) ===");
            foreach (var name in SpecificationEvaluator.Evaluate(gadgets, new GadgetNameSpec()))
                Console.WriteLine($"  {name}");
        }
    }
}
