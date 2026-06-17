namespace Sandbox.Core.Sql
{
    /// <summary>The result of translating a query: a SQL string and its parameter values.</summary>
    public sealed class CompiledSql
    {
        public string Sql { get; }
        public IReadOnlyDictionary<string, object?> Parameters { get; }

        public CompiledSql(string sql, IReadOnlyDictionary<string, object?> parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }

        public override string ToString() => Sql;
    }
}
