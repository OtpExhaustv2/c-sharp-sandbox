namespace Sandbox.Core.Specifications
{
    /// <summary>Applies a specification (filter, order, page, project) to an in-memory sequence.</summary>
    public static class SpecificationEvaluator
    {
        public static IEnumerable<T> Evaluate<T>(IEnumerable<T> source, Specification<T> spec)
        {
            var query = source;

            if (spec.Criteria is not null)
                query = query.Where(spec.Criteria.Compile());

            if (spec.OrderExpressions.Count > 0)
            {
                var first = spec.OrderExpressions[0];
                var ordered = first.Descending
                    ? query.OrderByDescending(first.KeySelector.Compile())
                    : query.OrderBy(first.KeySelector.Compile());

                for (var i = 1; i < spec.OrderExpressions.Count; i++)
                {
                    var next = spec.OrderExpressions[i];
                    ordered = next.Descending
                        ? ordered.ThenByDescending(next.KeySelector.Compile())
                        : ordered.ThenBy(next.KeySelector.Compile());
                }

                query = ordered;
            }

            if (spec.Skip.HasValue)
                query = query.Skip(spec.Skip.Value);

            if (spec.Take.HasValue)
                query = query.Take(spec.Take.Value);

            return query;
        }

        public static IEnumerable<TResult> Evaluate<T, TResult>(
            IEnumerable<T> source,
            Specification<T, TResult> spec)
        {
            if (spec.Selector is null)
                throw new InvalidOperationException("A projecting specification requires a Selector.");

            return Evaluate(source, (Specification<T>)spec).Select(spec.Selector.Compile());
        }
    }
}
