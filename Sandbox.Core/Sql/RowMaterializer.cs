using System.Data.Common;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Maps DbDataReader rows into schema-less dictionaries (column name → value),
    /// mapping DBNull to null. Works against any DbDataReader (a real SqlDataReader in
    /// production, a DataTable reader in tests). Used by both the buffered and streaming
    /// execution paths.
    /// </summary>
    public static class RowMaterializer
    {
        /// <summary>Maps the reader's current row to a dictionary.</summary>
        public static IReadOnlyDictionary<string, object?> MapRow(DbDataReader reader)
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            return row;
        }

        /// <summary>Reads every row and buffers them into a list.</summary>
        public static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> MaterializeAsync(
            DbDataReader reader,
            CancellationToken cancellationToken = default)
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            while (await reader.ReadAsync(cancellationToken))
                rows.Add(MapRow(reader));

            return rows;
        }
    }
}
