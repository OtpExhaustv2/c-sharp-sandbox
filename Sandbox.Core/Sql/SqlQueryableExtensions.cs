namespace Sandbox.Core.Sql
{
    public static class SqlQueryableExtensions
    {
        /// <summary>Translate the query into a parameterized SELECT (inspection; no execution).</summary>
        public static CompiledSql ToSql(this IQueryable query)
            => SqlTranslator.Translate(query.Expression, Provider(query).Table);

        /// <summary>Execute the query and buffer all rows.</summary>
        public static Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ToListAsync(
            this IQueryable query, CancellationToken cancellationToken = default)
        {
            var provider = ExecutableProvider(query);
            var compiled = SqlTranslator.Translate(query.Expression, provider.Table);
            return new SqlServerExecutor(provider.ConnectionString!).ExecuteAsync(compiled, cancellationToken);
        }

        /// <summary>Execute the query and stream rows one at a time.</summary>
        public static IAsyncEnumerable<IReadOnlyDictionary<string, object?>> AsAsyncEnumerable(
            this IQueryable query, CancellationToken cancellationToken = default)
        {
            var provider = ExecutableProvider(query);
            var compiled = SqlTranslator.Translate(query.Expression, provider.Table);
            return new SqlServerExecutor(provider.ConnectionString!).ExecuteStreamAsync(compiled, cancellationToken);
        }

        /// <summary>Execute and return the first row, or null if none.</summary>
        public static async Task<IReadOnlyDictionary<string, object?>?> FirstOrDefaultAsync(
            this IQueryable query, CancellationToken cancellationToken = default)
        {
            await foreach (var row in query.AsAsyncEnumerable(cancellationToken))
                return row;
            return null;
        }

        /// <summary>Translate the query into a parameterized SELECT COUNT(*) (inspection).</summary>
        public static CompiledSql ToCountSql(this IQueryable query)
            => SqlTranslator.TranslateCount(query.Expression, Provider(query).Table);

        /// <summary>Execute COUNT(*) for the query's filter.</summary>
        public static async Task<int> CountAsync(
            this IQueryable query, CancellationToken cancellationToken = default)
        {
            var provider = ExecutableProvider(query);
            var compiled = SqlTranslator.TranslateCount(query.Expression, provider.Table);
            var scalar = await new SqlServerExecutor(provider.ConnectionString!)
                .ExecuteScalarAsync(compiled, cancellationToken);
            return Convert.ToInt32(scalar);
        }

        /// <summary>True if any row matches the query's filter.</summary>
        public static async Task<bool> AnyAsync(
            this IQueryable query, CancellationToken cancellationToken = default)
            => await query.CountAsync(cancellationToken) > 0;

        private static SqlQueryProvider Provider(IQueryable query)
            => query.Provider as SqlQueryProvider
               ?? throw new NotSupportedException(
                   "This query was not created via SqlQuery.From or SqlServerQueryContext.Query.");

        private static SqlQueryProvider ExecutableProvider(IQueryable query)
        {
            var provider = Provider(query);
            if (provider.ConnectionString is null)
                throw new InvalidOperationException(
                    "This query is not bound to a connection. Use SqlServerQueryContext.Query(...), " +
                    "or call ToSql() to inspect the SQL.");
            return provider;
        }
    }
}
