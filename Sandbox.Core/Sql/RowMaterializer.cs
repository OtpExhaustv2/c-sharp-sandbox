using System.Data.Common;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Materializes the rows of a DbDataReader into schema-less dictionaries
    /// (column name → value), mapping DBNull to null. Works against any DbDataReader
    /// (a real SqlDataReader in production, a DataTable reader in tests).
    /// </summary>
    public static class RowMaterializer
    {
        public static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> MaterializeAsync(
            DbDataReader reader,
            CancellationToken cancellationToken = default)
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                        ? null
                        : reader.GetValue(i);
                rows.Add(row);
            }

            return rows;
        }
    }
}
