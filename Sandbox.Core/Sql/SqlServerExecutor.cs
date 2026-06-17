using System.Data;
using Microsoft.Data.SqlClient;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Executes a CompiledSql query against SQL Server and returns the rows as schema-less
    /// dictionaries. Read-only — the query builder only emits SELECT statements.
    /// </summary>
    public sealed class SqlServerExecutor
    {
        private readonly string _connectionString;

        public SqlServerExecutor(string connectionString) => _connectionString = connectionString;

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Execute(CompiledSql query)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = query.Sql;
            foreach (var (name, value) in query.Parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);

            using var reader = command.ExecuteReader();
            return RowMaterializer.Materialize(reader);
        }
    }
}
