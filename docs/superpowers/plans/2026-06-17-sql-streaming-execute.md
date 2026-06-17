# Streaming Execute Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `SqlServerExecutor.ExecuteStreamAsync` returning `IAsyncEnumerable<IReadOnlyDictionary<string,object?>>` (yields one row at a time) alongside the existing buffered `ExecuteAsync`, sharing a `RowMaterializer.MapRow` helper.

**Architecture:** Extract the per-row mapping into `RowMaterializer.MapRow(DbDataReader)`; `MaterializeAsync` reuses it. Add an async iterator on the executor that opens the connection/command/reader with `await using` and `yield return`s `MapRow(reader)` per `ReadAsync` — nothing buffered, resources disposed when enumeration ends.

**Tech Stack:** C# / .NET 10, `Microsoft.Data.SqlClient`, `System.Data.Common`, `IAsyncEnumerable`, MSTest.

**Spec:** `docs/superpowers/specs/2026-06-17-sql-streaming-execute-design.md`

---

## File Structure

- `Sandbox.Core/Sql/RowMaterializer.cs` — add `MapRow`; refactor `MaterializeAsync` to call it.
- `Sandbox.Core/Sql/SqlServerExecutor.cs` — add `ExecuteStreamAsync` async iterator.
- `Sandbox.Tests/Sql/RowMaterializerTests.cs` — add `MapRow` tests.
- `Sandbox/examples/SqlServerExamples.cs` — add a streaming `await foreach` section.

---

## Task 1: Extract `RowMaterializer.MapRow` (TDD)

**Files:**
- Modify: `Sandbox.Core/Sql/RowMaterializer.cs`
- Modify: `Sandbox.Tests/Sql/RowMaterializerTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `Sandbox.Tests/Sql/RowMaterializerTests.cs` (inside the `RowMaterializerTests` class):
```csharp
        [TestMethod]
        public void MapRow_MapsCurrentRowByName()
        {
            using var reader = BuildTable().CreateDataReader();
            Assert.IsTrue(reader.Read());

            var row = RowMaterializer.MapRow(reader);

            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual("Bolt", row["Name"]);
            Assert.AreEqual(5.00m, row["Price"]);
        }

        [TestMethod]
        public void MapRow_MapsDbNullToNull()
        {
            using var reader = BuildTable().CreateDataReader();
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read()); // second row has a DBNull Price

            var row = RowMaterializer.MapRow(reader);

            Assert.AreEqual("Nut", row["Name"]);
            Assert.IsNull(row["Price"]);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~MapRow"`
Expected: FAIL — `RowMaterializer.MapRow` not defined (compile error).

- [ ] **Step 3: Add `MapRow` and refactor `MaterializeAsync` to use it**

Replace the contents of `Sandbox.Core/Sql/RowMaterializer.cs` with:
```csharp
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
```

- [ ] **Step 4: Run the materializer tests to verify they pass**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~RowMaterializer"`
Expected: PASS — the 2 new `MapRow` tests plus the 3 existing `Materialize_*` tests all pass (the refactor is behavior-preserving).

- [ ] **Step 5: Commit**

```powershell
git add Sandbox.Core/Sql/RowMaterializer.cs Sandbox.Tests/Sql/RowMaterializerTests.cs
git commit -m "refactor(sql): extract RowMaterializer.MapRow (shared by buffered/streaming)"
```

---

## Task 2: `SqlServerExecutor.ExecuteStreamAsync`

No automated test (live-DB execution isn't CI-testable; the row mapping is covered by Task 1).
Verification is a clean build; the live path is exercised by the demo in Task 3.

**Files:**
- Modify: `Sandbox.Core/Sql/SqlServerExecutor.cs`

- [ ] **Step 1: Add the streaming iterator**

Replace the contents of `Sandbox.Core/Sql/SqlServerExecutor.cs` with:
```csharp
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
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Sandbox.Core/Sandbox.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add Sandbox.Core/Sql/SqlServerExecutor.cs
git commit -m "feat(sql): add SqlServerExecutor.ExecuteStreamAsync (IAsyncEnumerable)"
```

---

## Task 3: Streaming section in the console demo

**Files:**
- Modify: `Sandbox/examples/SqlServerExamples.cs`

- [ ] **Step 1: Add a streaming block to `RunQueriesAsync`**

In `Sandbox/examples/SqlServerExamples.cs`, append this block at the end of the
`RunQueriesAsync` method (after the existing "Available tools" query):
```csharp
            Console.WriteLine("\n=== Streaming: all products, one row at a time ===");
            var allProducts = SqlQuery.From("Products").OrderBy(r => r["Id"]).ToSql();
            Console.WriteLine($"SQL: {allProducts.Sql}");
            var streamed = 0;
            await foreach (var row in executor.ExecuteStreamAsync(allProducts))
            {
                Console.WriteLine("  " + string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}")));
                streamed++;
            }
            Console.WriteLine($"  (streamed {streamed} row(s))");
```

- [ ] **Step 2: Run the demo against the live instance**

Run: `dotnet run --project Sandbox/Sandbox.csproj`
Expected: the existing three sections print as before, then a fourth:
- "Streaming: all products, one row at a time": `SELECT * FROM [Products] ORDER BY [Id]` → all 6 seeded products printed one per line (Widget, Sprocket, Cog, Bolt, Nut, Hammer), ending with "(streamed 6 row(s))".

If a `SqlException` is printed, resolve connectivity (the existing catch prints guidance) and re-run.

- [ ] **Step 3: Commit**

```powershell
git add Sandbox/examples/SqlServerExamples.cs
git commit -m "feat(sql): stream all products in the console demo"
```

---

## Self-Review Notes (plan vs spec)

- **`RowMaterializer.MapRow` extraction + `MaterializeAsync` reuse** → Task 1, with `MapRow` unit tests; existing materializer tests confirm the refactor is behavior-preserving.
- **`ExecuteStreamAsync` async iterator** (`[EnumeratorCancellation]`, `await using`, `yield return MapRow`) → Task 2; buffered `ExecuteAsync` kept unchanged in the same file.
- **Shared mapping (DRY)** → both methods call `RowMaterializer.MapRow`.
- **Live streaming demo** → Task 3 (`await foreach`).
- Type/name consistency: `RowMaterializer.MapRow(DbDataReader)`, `MaterializeAsync`, `SqlServerExecutor.ExecuteAsync`/`ExecuteStreamAsync`, `CompiledSql`, `SqlQuery.From`/`.ToSql()` used identically across tasks.
- Note: `MaterializeAsync` now uses synchronous `IsDBNull` (via `MapRow`) on the already-read row instead of `IsDBNullAsync`; behavior is identical and the existing tests still pass.
