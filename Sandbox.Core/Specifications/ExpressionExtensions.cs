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
            var body = Expression.Not(Rebind(expression, parameter));
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private static Expression<Func<T, bool>> Combine<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right,
            Func<Expression, Expression, BinaryExpression> merge)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var leftBody = Rebind(left, parameter);
            var rightBody = Rebind(right, parameter);
            return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
        }

        // Rewrite a lambda's body so its single parameter is replaced by `parameter`,
        // leaving any nested lambda parameters (e.g. inside `.Any(n => ...)`) untouched.
        private static Expression Rebind<T>(Expression<Func<T, bool>> source, ParameterExpression parameter)
            => new ParameterReplacer(source.Parameters[0], parameter).Visit(source.Body)!;

        private sealed class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _target;
            private readonly ParameterExpression _replacement;

            public ParameterReplacer(ParameterExpression target, ParameterExpression replacement)
            {
                _target = target;
                _replacement = replacement;
            }

            protected override Expression VisitParameter(ParameterExpression node)
                => node == _target ? _replacement : node;
        }
    }
}
