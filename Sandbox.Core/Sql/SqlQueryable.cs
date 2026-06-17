using System.Collections;
using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// An IQueryable whose provider builds SQL instead of executing. Implements
    /// IOrderedQueryable so Queryable.OrderBy/ThenBy can chain (their internal cast to
    /// IOrderedQueryable&lt;T&gt; would otherwise fail).
    /// </summary>
    public sealed class SqlQueryable<T> : IOrderedQueryable<T>
    {
        public SqlQueryable(IQueryProvider provider, Expression? expression = null)
        {
            Provider = provider;
            Expression = expression ?? Expression.Constant(this);
        }

        public Type ElementType => typeof(T);
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
            => throw new NotSupportedException("This query builds SQL only; call ToSql().");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Entry point: start a query against a named table.</summary>
    public static class SqlQuery
    {
        public static IQueryable<IDictionary<string, object>> From(string table)
            => new SqlQueryable<IDictionary<string, object>>(new SqlQueryProvider(table));
    }
}
