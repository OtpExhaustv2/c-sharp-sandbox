namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Hands out connection-bound queries against SQL Server. The IQueryable returned by
    /// Query(table) supports terminal operators (ToListAsync, AsAsyncEnumerable,
    /// FirstOrDefaultAsync, CountAsync, AnyAsync) that translate and execute.
    /// </summary>
    public sealed class SqlServerQueryContext
    {
        private readonly string _connectionString;

        public SqlServerQueryContext(string connectionString) => _connectionString = connectionString;

        public IQueryable<IDictionary<string, object>> Query(string table)
            => new SqlQueryable<IDictionary<string, object>>(new SqlQueryProvider(table, _connectionString));
    }
}
