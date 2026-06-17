# SQL Server Execution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the `CompiledSql` produced by the LINQ-to-SQL builder against a local SQL Server and materialize result rows into `IDictionary<string,object>`, proven by a runnable console demo against `DESKTOP-424PEIH\SQLEXPRESS`.

**Architecture:** `Sandbox.Core/Sql/` gains a `RowMaterializer` (maps any `IDataReader` → list of column-name→value dictionaries, `DBNull`→`null`) and a `SqlServerExecutor` (opens a `SqlConnection`, binds `CompiledSql` parameters, runs `ExecuteReader`, materializes). A console example bootstraps a demo DB/table idempotently, builds queries with the existing builder, executes them, and prints the rows.

**Tech Stack:** C# / .NET 10, `Microsoft.Data.SqlClient`, `System.Data` (`IDataReader`/`DataTable`), MSTest.

**Spec:** `docs/superpowers/specs/2026-06-17-sql-server-execution-design.md`

---

## File Structure

- `Sandbox.Core/Sandbox.Core.csproj` — add `Microsoft.Data.SqlClient` package reference.
- `Sandbox.Core/Sql/RowMaterializer.cs` (new) — `IDataReader` → rows. Pure, unit-testable.
- `Sandbox.Core/Sql/SqlServerExecutor.cs` (new) — `CompiledSql` → rows against SQL Server.
- `Sandbox.Tests/RowMaterializerTests.cs` (new) — materializer tests via `DataTable.CreateDataReader()`.
- `Sandbox/Sandbox.csproj` — add `Microsoft.Data.SqlClient` (the demo uses it directly).
- `Sandbox/examples/SqlServerExamples.cs` (new) — bootstrap + build/execute/print demo.
- `Sandbox/Program.cs` — call `SqlServerExamples.Main()`.

`Sandbox.Tests` already references `Sandbox.Core`; `System.Data` is in-box.

---

## Task 1: `RowMaterializer` (IDataReader → dictionaries)

**Files:**
- Modify: `Sandbox.Core/Sandbox.Core.csproj`
- Create: `Sandbox.Core/Sql/RowMaterializer.cs`
- Create: `Sandbox.Tests/RowMaterializerTests.cs`

- [ ] **Step 1: Add the SQL Server package to Sandbox.Core**

Run:
```powershell
dotnet add Sandbox.Core/Sandbox.Core.csproj package Microsoft.Data.SqlClient
```
Expected: package added; `dotnet build Sandbox.Core/Sandbox.Core.csproj` succeeds.

- [ ] **Step 2: Write the failing tests**

Create `Sandbox.Tests/RowMaterializerTests.cs`:
```csharp
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Sql;

namespace Sandbox.Tests
{
    [TestClass]
    public class RowMaterializerTests
    {
        private static DataTable BuildTable()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Price", typeof(decimal));
            table.Rows.Add(1, "Bolt", 5.00m);
            table.Rows.Add(2, "Nut", DBNull.Value);
            return table;
        }

        [TestMethod]
        public void Materialize_MapsColumnsToDictionaryByName()
        {
            using var reader = BuildTable().CreateDataReader();

            var rows = RowMaterializer.Materialize(reader);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1, rows[0]["Id"]);
            Assert.AreEqual("Bolt", rows[0]["Name"]);
            Assert.AreEqual(5.00m, rows[0]["Price"]);
        }

        [TestMethod]
        public void Materialize_MapsDbNullToNull()
        {
            using var reader = BuildTable().CreateDataReader();

            var rows = RowMaterializer.Materialize(reader);

            Assert.AreEqual("Nut", rows[1]["Name"]);
            Assert.IsNull(rows[1]["Price"]);
        }

        [TestMethod]
        public void Materialize_EmptyReader_ReturnsEmpty()
        {
            var empty = new DataTable();
            empty.Columns.Add("Id", typeof(int));
            using var reader = empty.CreateDataReader();

            var rows = RowMaterializer.Materialize(reader);

            Assert.IsEmpty(rows);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~RowMaterializer"`
Expected: FAIL — `RowMaterializer` not defined (compile error).

- [ ] **Step 4: Implement `RowMaterializer`**

Create `Sandbox.Core/Sql/RowMaterializer.cs`:
```csharp
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~RowMaterializer"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```powershell
git add Sandbox.Core/Sandbox.Core.csproj Sandbox.Core/Sql/RowMaterializer.cs Sandbox.Tests/RowMaterializerTests.cs
git commit -m "feat(sql): add RowMaterializer (IDataReader -> dictionaries, DBNull -> null)"
```

---

## Task 2: `SqlServerExecutor` (CompiledSql → rows)

No automated test: executing requires a live SQL Server, which CI does not have. The pure
mapping is covered by Task 1; this executor is verified by the demo in Task 3. Verification
here is a successful build.

**Files:**
- Create: `Sandbox.Core/Sql/SqlServerExecutor.cs`

- [ ] **Step 1: Implement the executor**

Create `Sandbox.Core/Sql/SqlServerExecutor.cs`:
```csharp
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
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Sandbox.Core/Sandbox.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```powershell
git add Sandbox.Core/Sql/SqlServerExecutor.cs
git commit -m "feat(sql): add SqlServerExecutor (CompiledSql -> rows)"
```

---

## Task 3: Console demo — bootstrap DB, execute built queries, print rows

**Files:**
- Modify: `Sandbox/Sandbox.csproj`
- Create: `Sandbox/examples/SqlServerExamples.cs`
- Modify: `Sandbox/Program.cs`

- [ ] **Step 1: Add the SQL Server package to the console project**

Run:
```powershell
dotnet add Sandbox/Sandbox.csproj package Microsoft.Data.SqlClient
```
Expected: package added (the demo uses `SqlConnection` directly for the idempotent bootstrap).

- [ ] **Step 2: Create the demo**

Create `Sandbox/examples/SqlServerExamples.cs`:
```csharp
using Microsoft.Data.SqlClient;
using Sandbox.Core.Sql;

namespace Sandbox.examples
{
    public class SqlServerExamples
    {
        private const string Server = @"DESKTOP-424PEIH\SQLEXPRESS";
        private const string Database = "SandboxLinqSql";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;Encrypt=False";
        private static string DbConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;Encrypt=False";

        public static void Main()
        {
            try
            {
                Bootstrap();
                RunQueries();
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Could not reach SQL Server at {Server}: {ex.Message}");
                Console.WriteLine("Is the SQLEXPRESS instance running, with TCP/IP or named pipes enabled?");
            }
        }

        private static void RunQueries()
        {
            var executor = new SqlServerExecutor(DbConnectionString);

            Console.WriteLine("=== Price > 50, cheapest first ===");
            Print(SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .OrderBy(r => r["Price"])
                .ToSql(), executor);

            Console.WriteLine("\n=== Name contains 'o', top 2 by price desc ===");
            Print(SqlQuery.From("Products")
                .Where(r => ((string)r["Name"]).Contains("o"))
                .OrderByDescending(r => r["Price"])
                .Take(2)
                .ToSql(), executor);

            Console.WriteLine("\n=== Available tools, projected to Name + Price ===");
            Print(SqlQuery.From("Products")
                .Where(r => (string)r["Category"] == "Tools" && (bool)r["IsAvailable"] == true)
                .Select(r => new { Name = r["Name"], Price = r["Price"] })
                .ToSql(), executor);
        }

        private static void Print(CompiledSql query, SqlServerExecutor executor)
        {
            Console.WriteLine($"SQL: {query.Sql}");
            var rows = executor.Execute(query);
            foreach (var row in rows)
                Console.WriteLine("  " + string.Join(", ", row.Select(kv => $"{kv.Key}={kv.Value}")));
            Console.WriteLine($"  ({rows.Count} row(s))");
        }

        private static void Bootstrap()
        {
            using (var master = new SqlConnection(MasterConnectionString))
            {
                master.Open();
                ExecuteNonQuery(master, $"IF DB_ID('{Database}') IS NULL CREATE DATABASE [{Database}];");
            }

            using var db = new SqlConnection(DbConnectionString);
            db.Open();

            ExecuteNonQuery(db, """
                IF OBJECT_ID('dbo.Products', 'U') IS NULL
                CREATE TABLE [Products] (
                    [Id]            INT            NOT NULL PRIMARY KEY,
                    [Name]          NVARCHAR(100)  NOT NULL,
                    [Price]         DECIMAL(18,2)  NOT NULL,
                    [Category]      NVARCHAR(50)   NOT NULL,
                    [StockQuantity] INT            NOT NULL,
                    [IsAvailable]   BIT            NOT NULL
                );
                """);

            // Deterministic re-seed so repeated runs are idempotent.
            ExecuteNonQuery(db, "DELETE FROM [Products];");
            ExecuteNonQuery(db, """
                INSERT INTO [Products] ([Id],[Name],[Price],[Category],[StockQuantity],[IsAvailable]) VALUES
                    (1, N'Widget',   30.00, N'Tools',     12, 1),
                    (2, N'Sprocket', 10.00, N'Tools',      0, 0),
                    (3, N'Cog',      75.00, N'Tools',      5, 1),
                    (4, N'Bolt',      5.00, N'Hardware', 200, 1),
                    (5, N'Nut',       2.00, N'Hardware', 500, 1),
                    (6, N'Hammer',   55.00, N'Tools',      8, 1);
                """);

            Console.WriteLine($"Database [{Database}] ready with seeded [Products].\n");
        }

        private static void ExecuteNonQuery(SqlConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
```

- [ ] **Step 3: Wire the demo into `Program.cs`**

Replace the contents of `Sandbox/Program.cs` with:
```csharp
using Sandbox.examples;

SqlServerExamples.Main();
```

- [ ] **Step 4: Run the demo against the live instance**

Run: `dotnet run --project Sandbox/Sandbox.csproj`
Expected output (rows depend on the seed; spot-check):
- "Price > 50, cheapest first": `SELECT * FROM [Products] WHERE [Price] > @p0 ORDER BY [Price]` → Hammer (55.00) then Cog (75.00), 2 rows.
- "Name contains 'o', top 2 by price desc": SQL has `[Name] LIKE @p0 ... ORDER BY [Price] DESC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY` → Cog (75.00) then Sprocket (10.00), 2 rows.
- "Available tools, projected": `SELECT [Name], [Price] FROM [Products] WHERE ([Category] = @p0 AND [IsAvailable] = @p1)` → Widget, Cog, Hammer (Sprocket excluded — not available), 3 rows.

If a `SqlException` is printed instead, the instance/connection needs attention (the catch prints guidance); resolve connectivity and re-run.

- [ ] **Step 5: Commit**

```powershell
git add Sandbox/Sandbox.csproj Sandbox/examples/SqlServerExamples.cs Sandbox/Program.cs
git commit -m "feat(sql): runnable SQL Server demo (bootstrap, execute built queries, print rows)"
```

---

## Self-Review Notes (plan vs spec)

- **`Microsoft.Data.SqlClient` on `Sandbox.Core`** → Task 1 Step 1. Also added to the console (Task 3 Step 1) since the demo uses `SqlConnection` directly.
- **`RowMaterializer` over `IDataReader`, DBNull→null** → Task 1, unit-tested via `DataTable.CreateDataReader()` (no live DB).
- **`SqlServerExecutor.Execute(CompiledSql)`** (connection, `AddWithValue` with `null`→`DBNull`, reader → materializer) → Task 2.
- **Idempotent bootstrap** (create DB via `master`, create table if absent, deterministic re-seed) → Task 3 `Bootstrap`.
- **Build→execute→print demo** with the three query shapes; friendly `SqlException` catch → Task 3.
- **Connection string** (integrated auth, `Encrypt=False`, machine-specific server in one constant) → Task 3 constants.
- **Result shape** `IReadOnlyList<IReadOnlyDictionary<string,object?>>` — consistent across `RowMaterializer` and `SqlServerExecutor`.
- Type/name consistency: `RowMaterializer.Materialize(IDataReader)`, `SqlServerExecutor(string)` + `.Execute(CompiledSql)`, `SqlQuery.From`/`.ToSql()` (existing) used identically across tasks.
- **Deviation from strict TDD** in Task 2 (no test) is intentional and justified in the task preamble: live-DB execution is not CI-testable; the testable mapping is covered in Task 1 and the executor is exercised by the Task 3 run.
