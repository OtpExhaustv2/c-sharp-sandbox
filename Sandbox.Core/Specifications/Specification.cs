using System.Linq.Expressions;

namespace Sandbox.Core.Specifications
{
    /// <summary>
    /// A query specification: an optional predicate (null = match all) plus optional
    /// ordering and paging. Composes with And/Or/Not. Evaluate via SpecificationEvaluator.
    /// </summary>
    public abstract class Specification<T>
    {
        public Expression<Func<T, bool>>? Criteria { get; private set; }

        private readonly List<(Expression<Func<T, object>> KeySelector, bool Descending)> _orderExpressions = new();

        public IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions
            => _orderExpressions;

        public int? Skip { get; private set; }
        public int? Take { get; private set; }

        protected Specification() { }

        protected Specification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

        protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

        protected void AddOrderBy(Expression<Func<T, object>> keySelector)
            => _orderExpressions.Add((keySelector, false));

        protected void AddOrderByDescending(Expression<Func<T, object>> keySelector)
            => _orderExpressions.Add((keySelector, true));

        protected void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        // Compiles the criteria on each call. Fine for one-off checks; for filtering a
        // sequence prefer SpecificationEvaluator.Evaluate, which compiles once per query.
        public bool IsSatisfiedBy(T entity)
            => Criteria is null || Criteria.Compile()(entity);

        public Specification<T> And(Specification<T> other)
            => WithShapingFrom(this, EffectiveCriteria(this).And(EffectiveCriteria(other)));

        public Specification<T> Or(Specification<T> other)
            => WithShapingFrom(this, EffectiveCriteria(this).Or(EffectiveCriteria(other)));

        public Specification<T> Not()
            => WithShapingFrom(this, EffectiveCriteria(this).Not());

        private static Expression<Func<T, bool>> EffectiveCriteria(Specification<T> spec)
        {
            Expression<Func<T, bool>> matchAll = _ => true;
            return spec.Criteria ?? matchAll;
        }

        // Combinators keep the LEFT spec's ordering/paging; the right operand contributes
        // only its criteria. (Combining paging across specs is undefined, so it is avoided.)
        private static Specification<T> WithShapingFrom(
            Specification<T> source,
            Expression<Func<T, bool>> criteria)
        {
            var spec = new AdHocSpecification<T>(criteria);
            spec._orderExpressions.AddRange(source._orderExpressions);
            if (source.Skip.HasValue && source.Take.HasValue)
                spec.ApplyPaging(source.Skip.Value, source.Take.Value);
            return spec;
        }
    }

    /// <summary>Concrete specification produced by And/Or/Not composition.</summary>
    internal sealed class AdHocSpecification<T> : Specification<T>
    {
        public AdHocSpecification(Expression<Func<T, bool>> criteria) : base(criteria) { }
    }

    /// <summary>A specification that also projects matched entities to <typeparamref name="TResult"/>.</summary>
    public abstract class Specification<T, TResult> : Specification<T>
    {
        public Expression<Func<T, TResult>>? Selector { get; private set; }

        protected Specification() { }

        protected Specification(Expression<Func<T, bool>> criteria) : base(criteria) { }

        protected void AddSelector(Expression<Func<T, TResult>> selector) => Selector = selector;
    }
}
