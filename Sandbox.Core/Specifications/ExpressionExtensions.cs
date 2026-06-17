using System.Linq.Expressions;

namespace Sandbox.Core.Specifications
{
    /// <summary>
    /// Combines predicate expressions by rebinding their parameters onto a single shared
    /// parameter, so the resulting tree is a valid single-parameter lambda (and would
    /// translate to SQL by a query provider, unlike Expression.Invoke).
    /// </summary>
    public static class ExpressionExtensions
    {
        public static Expression<Func<T, bool>> And<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
            => Combine(left, right, Expression.AndAlso);

        public static Expression<Func<T, bool>> Or<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
            => Combine(left, right, Expression.OrElse);

        public static Expression<Func<T, bool>> Not<T>(
            this Expression<Func<T, bool>> expression)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var body = Expression.Not(new ParameterReplacer(parameter).Visit(expression.Body)!);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private static Expression<Func<T, bool>> Combine<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right,
            Func<Expression, Expression, BinaryExpression> merge)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var leftBody = new ParameterReplacer(parameter).Visit(left.Body)!;
            var rightBody = new ParameterReplacer(parameter).Visit(right.Body)!;
            return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
        }

        private sealed class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;

            public ParameterReplacer(ParameterExpression parameter) => _parameter = parameter;

            protected override Expression VisitParameter(ParameterExpression node) => _parameter;
        }
    }
}
