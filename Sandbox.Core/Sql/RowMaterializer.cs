using System.Data;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Materializes the rows of an IDataReader into schema-less dictionaries
    /// (column name → value), mapping DBNull to null. Works against any IDataReader
    /// (real SqlDataReader in production, a DataTable reader in tests).
    /// </summary>
    public static class RowMaterializer
    {
        public static IReadOnlyList<IReadOnlyDictionary<string, object?>> Materialize(IDataReader reader)
        {
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            return rows;
        }
    }
}
