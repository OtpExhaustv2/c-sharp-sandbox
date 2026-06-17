using System.Runtime.CompilerServices;
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

        /// <summary>Executes the query and buffers all rows into a list.</summary>
        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteAsync(
            CompiledSql query,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = query.Sql;
            foreach (var (name, value) in query.Parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await RowMaterializer.MaterializeAsync(reader, cancellationToken);
        }

        /// <summary>
        /// Executes the query and streams rows one at a time. The connection/command/reader
        /// stay open for the lifetime of enumeration and are disposed when the consumer
        /// finishes or breaks early — nothing is buffered.
        /// </summary>
        public async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteStreamAsync(
            CompiledSql query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = query.Sql;
            foreach (var (name, value) in query.Parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                yield return RowMaterializer.MapRow(reader);
        }
    }
}
