namespace Sandbox.Core.Sql
{
    public static class SqlQueryableExtensions
    {
        /// <summary>Translate the built query into a parameterized SQL Server SELECT.</summary>
        public static CompiledSql ToSql(this IQueryable query)
        {
            if (query.Provider is not SqlQueryProvider provider)
                throw new NotSupportedException("ToSql requires a query created via SqlQuery.From.");

            return SqlTranslator.Translate(query.Expression, provider.Table);
        }
    }
}
