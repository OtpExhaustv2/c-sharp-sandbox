using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Builds the LINQ expression chain (via CreateQuery) but never executes — the query is
    /// turned into SQL by ToSql(). Carries the root table name.
    /// </summary>
    internal sealed class SqlQueryProvider : IQueryProvider
    {
        public string Table { get; }

        public SqlQueryProvider(string table) => Table = table;

        public IQueryable CreateQuery(Expression expression)
            => new SqlQueryable<IDictionary<string, object>>(this, expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new SqlQueryable<TElement>(this, expression);

        public object Execute(Expression expression)
            => throw new NotSupportedException("This provider builds SQL only; call ToSql().");

        public TResult Execute<TResult>(Expression expression)
            => throw new NotSupportedException("This provider builds SQL only; call ToSql().");
    }
}
