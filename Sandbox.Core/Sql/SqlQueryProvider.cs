using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Builds the LINQ expression chain (via CreateQuery). When created with a connection
    /// string (by SqlServerQueryContext), its queries become executable via the terminal
    /// operators; otherwise they are translate-only (ToSql).
    /// </summary>
    internal sealed class SqlQueryProvider : IQueryProvider
    {
        public string Table { get; }
        public string? ConnectionString { get; }

        public SqlQueryProvider(string table, string? connectionString = null)
        {
            Table = table;
            ConnectionString = connectionString;
        }

        public IQueryable CreateQuery(Expression expression)
            => new SqlQueryable<IDictionary<string, object>>(this, expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new SqlQueryable<TElement>(this, expression);

        public object Execute(Expression expression)
            => throw new NotSupportedException(
                "Use a terminal operator (ToListAsync, etc.) on a context query, or call ToSql().");

        public TResult Execute<TResult>(Expression expression)
            => throw new NotSupportedException(
                "Use a terminal operator (ToListAsync, etc.) on a context query, or call ToSql().");
    }
}
