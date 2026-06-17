# Executable Query Context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `SqlServerQueryContext` that hands out connection-bound queries with async terminal operators (`ToListAsync`, `AsAsyncEnumerable`, `FirstOrDefaultAsync`, `CountAsync`, `AnyAsync`), so LINQ queries execute directly instead of via a manually-constructed executor.

**Architecture:** `SqlQueryProvider` gains an optional `ConnectionString`; `SqlServerQueryContext.Query(table)` creates a query bound to it. Terminal operators are extensions on `IQueryable` that resolve the bound provider, translate (existing `SqlTranslator`), and run through `SqlServerExecutor` (the internal engine). Aggregates use a new `ToCountSql()` (`SELECT COUNT(*)`) + a scalar execution path.

**Tech Stack:** C# / .NET 10, `Microsoft.Data.SqlClient`, `IAsyncEnumerable`, MSTest.

**Spec:** `docs/superpowers/specs/2026-06-17-executable-query-context-design.md`

---

## File Structure

- `Sandbox.Core/Sql/SqlQueryProvider.cs` — add optional `ConnectionString`.
- `Sandbox.Core/Sql/SqlServerQueryContext.cs` (NEW) — `Query(table)` → bound `IQueryable`.
- `Sandbox.Core/Sql/SqlQueryableExtensions.cs` — add terminal operators + `ToCountSql` + guard helpers.
- `Sandbox.Core/Sql/SqlServerExecutor.cs` — add `ExecuteScalarAsync`; extract `CreateCommand`.
- `Sandbox.Core/Sql/SqlTranslator.cs` — add `TranslateCount`/`RenderCount`; extract `AppendWhere`.
- `Sandbox.Tests/Sql/ExecutableQueryTests.cs` (NEW) — guard + `ToCountSql` unit tests (no DB).
- `Sandbox/examples/SqlServerExamples.cs` — add a context demo section.

---

## Task 1: Connection binding + `SqlServerQueryContext` + row terminal operators

**Files:**
- Modify: `Sandbox.Core/Sql/SqlQueryProvider.cs`
- Create: `Sandbox.Core/Sql/SqlServerQueryContext.cs`
- Modify: `Sandbox.Core/Sql/SqlQueryableExtensions.cs`
- Create: `Sandbox.Tests/Sql/ExecutableQueryTests.cs`

- [ ] **Step 1: Write the failing guard test**

Create `Sandbox.Tests/Sql/ExecutableQueryTests.cs`:
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Sql;

namespace Sandbox.Tests.Sql
{
    [TestClass]
    public class ExecutableQueryTests
    {
        [TestMethod]
        public async Task ToListAsync_OnUnboundQuery_Throws()
        {
            // A query from SqlQuery.From is not bound to a connection.
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => SqlQuery.From("Products").ToListAsync());
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~ExecutableQueryTests"`
Expected: FAIL — `ToListAsync`/`CountAsync` not defined (compile error).

- [ ] **Step 3: Add the optional connection string to `SqlQueryProvider`**

Replace the contents of `Sandbox.Core/Sql/SqlQueryProvider.cs` with:
```csharp
using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Builds the LINQ expression chain (via CreateQuery). When created with a connection
    /// string (by SqlServerQueryContext), its queries become executable via the terminal
    /// operators; otherwise they are translate-only (ToSql).
    /// </summary>
    internal sealed class SqlQueryProvider : IQueryProvider
    {
        public string Table { get; }
        public string? ConnectionString { get; }

        public SqlQueryProvider(string table, string? connectionString = null)
        {
            Table = table;
            ConnectionString = connectionString;
        }

        public IQueryable CreateQuery(Expression expression)
            => new SqlQueryable<IDictionary<string, object>>(this, expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new SqlQueryable<TElement>(this, expression);

        public object Execute(Expression expression)
            => throw new NotSupportedException(
                "Use a terminal operator (ToListAsync, etc.) on a context query, or call ToSql().");

        public TResult Execute<TResult>(Expression expression)
            => throw new NotSupportedException(
                "Use a terminal operator (ToListAsync, etc.) on a context query, or call ToSql().");
    }
}
```

- [ ] **Step 4: Create `SqlServerQueryContext`**

Create `Sandbox.Core/Sql/SqlServerQueryContext.cs`:
```csharp
namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Hands out connection-bound queries against SQL Server. The IQueryable returned by
    /// Query(table) supports terminal operators (ToListAsync, AsAsyncEnumerable,
    /// FirstOrDefaultAsync, CountAsync, AnyAsync) that translate and execute.
    /// </summary>
    public sealed class SqlServerQueryContext
    {
        private readonly string _connectionString;

        public SqlServerQueryContext(string connectionString) => _connectionString = connectionString;

        public IQueryable<IDictionary<string, object>> Query(string table)
            => new SqlQueryable<IDictionary<string, object>>(new SqlQueryProvider(table, _connectionString));
    }
}
```

- [ ] **Step 5: Add the row terminal operators + guard helpers**

Replace the contents of `Sandbox.Core/Sql/SqlQueryableExtensions.cs` with:
```csharp
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
```

- [ ] **Step 6: Run the guard test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~ExecutableQueryTests"`
Expected: PASS — `ToListAsync_OnUnboundQuery_Throws`.

- [ ] **Step 7: Run the full suite (confirm nothing regressed)**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — all tests green (existing 42 + 1 new guard test = 43).

- [ ] **Step 8: Commit**

```powershell
git add Sandbox.Core/Sql/SqlQueryProvider.cs Sandbox.Core/Sql/SqlServerQueryContext.cs Sandbox.Core/Sql/SqlQueryableExtensions.cs Sandbox.Tests/Sql/ExecutableQueryTests.cs
git commit -m "feat(sql): connection-bound query context + row terminal operators"
```

---

## Task 2: `CountAsync`/`AnyAsync` + `ToCountSql` + scalar execution

**Files:**
- Modify: `Sandbox.Core/Sql/SqlTranslator.cs`
- Modify: `Sandbox.Core/Sql/SqlServerExecutor.cs`
- Modify: `Sandbox.Core/Sql/SqlQueryableExtensions.cs`
- Modify: `Sandbox.Tests/Sql/ExecutableQueryTests.cs`

- [ ] **Step 1: Write the failing `ToCountSql` tests**

Add to `Sandbox.Tests/Sql/ExecutableQueryTests.cs` (inside the `ExecutableQueryTests` class):
```csharp
        [TestMethod]
        public async Task CountAsync_OnUnboundQuery_Throws()
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => SqlQuery.From("Products").CountAsync());
        }

        [TestMethod]
        public void ToCountSql_BareTable()
        {
            var compiled = SqlQuery.From("Products").ToCountSql();

            Assert.AreEqual("SELECT COUNT(*) FROM [Products]", compiled.Sql);
            Assert.IsEmpty(compiled.Parameters);
        }

        [TestMethod]
        public void ToCountSql_WithWhere()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .ToCountSql();

            Assert.AreEqual("SELECT COUNT(*) FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void ToCountSql_WithPaging_Throws()
        {
            Assert.ThrowsExactly<NotSupportedException>(
                () => SqlQuery.From("Products").Skip(10).Take(5).ToCountSql());
        }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~ToCountSql"`
Expected: FAIL — `ToCountSql` not defined (compile error).

- [ ] **Step 3: Add `TranslateCount`/`RenderCount` and extract `AppendWhere` in `SqlTranslator`**

In `Sandbox.Core/Sql/SqlTranslator.cs`, replace the `Translate` method and the `Render` method's WHERE block, and add the new members. Concretely:

(a) Replace the existing `Translate` method with these three members (extracting `BuildModel` and adding `TranslateCount`):
```csharp
        public static CompiledSql Translate(Expression expression, string table)
        {
            var (model, parameters) = BuildModel(expression, table);
            return new CompiledSql(Render(model, parameters), parameters.ToDictionary());
        }

        public static CompiledSql TranslateCount(Expression expression, string table)
        {
            var (model, parameters) = BuildModel(expression, table);
            return new CompiledSql(RenderCount(model), parameters.ToDictionary());
        }

        private static (SqlQueryModel Model, SqlParameterBag Parameters) BuildModel(
            Expression expression, string table)
        {
            var model = new SqlQueryModel { Table = table };
            var parameters = new SqlParameterBag();

            var calls = new Stack<MethodCallExpression>();
            var current = expression;
            while (current is MethodCallExpression call)
            {
                calls.Push(call);
                current = call.Arguments[0];
            }

            foreach (var call in calls)
                Apply(call, model, parameters);

            return (model, parameters);
        }
```

(b) In the existing `Render` method, replace the WHERE block:
```csharp
            if (m.WhereFragments.Count > 0)
            {
                // A single fragment is already self-contained; multiple Where() calls are
                // AND-combined and each wrapped so their internal precedence can't leak.
                var where = m.WhereFragments.Count == 1
                    ? m.WhereFragments[0]
                    : string.Join(" AND ", m.WhereFragments.Select(f => $"({f})"));
                sb.Append(" WHERE ").Append(where);
            }
```
with a call to the shared helper:
```csharp
            AppendWhere(sb, m);
```

(c) Add `RenderCount` and `AppendWhere` (e.g. after `Render`):
```csharp
        private static string RenderCount(SqlQueryModel m)
        {
            if (m.Skip.HasValue || m.Take.HasValue)
                throw new NotSupportedException("CountAsync does not support Skip/Take in v1.");

            var sb = new StringBuilder();
            sb.Append("SELECT COUNT(*) FROM [").Append(m.Table).Append(']');
            AppendWhere(sb, m);
            return sb.ToString();
        }

        // Appends " WHERE <fragment>" (multiple Where() calls are AND-combined, each wrapped).
        private static void AppendWhere(StringBuilder sb, SqlQueryModel m)
        {
            if (m.WhereFragments.Count == 0)
                return;

            var where = m.WhereFragments.Count == 1
                ? m.WhereFragments[0]
                : string.Join(" AND ", m.WhereFragments.Select(f => $"({f})"));
            sb.Append(" WHERE ").Append(where);
        }
```

- [ ] **Step 4: Add `ExecuteScalarAsync` + `CreateCommand` to `SqlServerExecutor`**

Replace the contents of `Sandbox.Core/Sql/SqlServerExecutor.cs` with:
```csharp
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Executes a CompiledSql query against SQL Server and returns the rows as schema-less
    /// dictionaries (or a scalar). Read-only — the query builder only emits SELECT statements.
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

            await using var command = CreateCommand(connection, query);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await RowMaterializer.MaterializeAsync(reader, cancellationToken);
        }

        /// <summary>Executes the query and streams rows one at a time.</summary>
        public async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteStreamAsync(
            CompiledSql query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, query);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                yield return RowMaterializer.MapRow(reader);
        }

        /// <summary>Executes the query and returns the first column of the first row.</summary>
        public async Task<object?> ExecuteScalarAsync(
            CompiledSql query,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = CreateCommand(connection, query);
            return await command.ExecuteScalarAsync(cancellationToken);
        }

        private static SqlCommand CreateCommand(SqlConnection connection, CompiledSql query)
        {
            var command = connection.CreateCommand();
            command.CommandText = query.Sql;
            foreach (var (name, value) in query.Parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return command;
        }
    }
}
```

- [ ] **Step 5: Add `ToCountSql`, `CountAsync`, `AnyAsync` to the extensions**

In `Sandbox.Core/Sql/SqlQueryableExtensions.cs`, add these methods to the `SqlQueryableExtensions` class (e.g. after `FirstOrDefaultAsync`):
```csharp
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
```

- [ ] **Step 6: Run the unit tests**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~ExecutableQueryTests"`
Expected: PASS — 5 tests: `ToListAsync_OnUnboundQuery_Throws` (Task 1), `CountAsync_OnUnboundQuery_Throws`, `ToCountSql_BareTable`, `ToCountSql_WithWhere`, `ToCountSql_WithPaging_Throws`.

- [ ] **Step 7: Run the full suite (confirm nothing regressed)**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — all tests green (the existing 42 + 5 new = 47), confirming the `Render`/`AppendWhere` refactor is behavior-preserving.

- [ ] **Step 8: Commit**

```powershell
git add Sandbox.Core/Sql/SqlTranslator.cs Sandbox.Core/Sql/SqlServerExecutor.cs Sandbox.Core/Sql/SqlQueryableExtensions.cs Sandbox.Tests/Sql/ExecutableQueryTests.cs
git commit -m "feat(sql): CountAsync/AnyAsync via ToCountSql + scalar execution"
```

---

## Task 3: Context demo section + live run

**Files:**
- Modify: `Sandbox/examples/SqlServerExamples.cs`

- [ ] **Step 1: Add a context section to `RunQueriesAsync`**

In `Sandbox/examples/SqlServerExamples.cs`, append this block at the end of the `RunQueriesAsync` method:
```csharp
            Console.WriteLine("\n=== Context (DbContext-lite): execute directly from the query ===");
            var ctx = new SqlServerQueryContext(DbConnectionString);

            var availableTools = ctx.Query("Products")
                .Where(r => (string)r["Category"] == "Tools" && (bool)r["IsAvailable"] == true);

            var toolList = await availableTools.ToListAsync();
            Console.WriteLine($"ToListAsync: {toolList.Count} available tool(s)");

            var cheapest = await ctx.Query("Products").OrderBy(r => r["Price"]).FirstOrDefaultAsync();
            Console.WriteLine($"FirstOrDefaultAsync cheapest: {cheapest?["Name"]} ({cheapest?["Price"]})");

            var availableToolCount = await availableTools.CountAsync();
            Console.WriteLine($"CountAsync available tools: {availableToolCount}");

            var anyHardware = await ctx.Query("Products").Where(r => (string)r["Category"] == "Hardware").AnyAsync();
            Console.WriteLine($"AnyAsync hardware: {anyHardware}");

            Console.WriteLine("Streaming via context:");
            await foreach (var row in ctx.Query("Products").OrderByDescending(r => r["Price"]).AsAsyncEnumerable())
                Console.WriteLine($"  {row["Name"]} = {row["Price"]}");
```

- [ ] **Step 2: Run the demo against the live instance**

Run: `dotnet run --project Sandbox/Sandbox.csproj`
Expected: the prior sections print as before, then a "Context (DbContext-lite)" section:
- `ToListAsync: 3 available tool(s)` (Widget, Cog, Hammer).
- `FirstOrDefaultAsync cheapest: Nut (2,00)` (locale decimal separator may vary).
- `CountAsync available tools: 3`.
- `AnyAsync hardware: True`.
- Streaming lists all 6 products, price descending (Cog, Hammer, Widget, Sprocket, Bolt, Nut).

If a `SqlException` prints, resolve connectivity (the existing catch prints guidance) and re-run.

- [ ] **Step 3: Commit**

```powershell
git add Sandbox/examples/SqlServerExamples.cs
git commit -m "feat(sql): demonstrate the executable query context end to end"
```

---

## Self-Review Notes (plan vs spec)

- **Optional `ConnectionString` binding** → Task 1 (`SqlQueryProvider`), preserved across operators by `CreateQuery` passing `this`.
- **`SqlServerQueryContext.Query`** → Task 1.
- **Row terminals** (`ToListAsync`, `AsAsyncEnumerable`, `FirstOrDefaultAsync`) + unbound-query guard → Task 1; guard unit-tested (no DB).
- **`ExecuteScalarAsync` + `CreateCommand` DRY** → Task 2.
- **`TranslateCount`/`RenderCount`, throws on Skip/Take; `AppendWhere` shared with `Render`** → Task 2; `ToCountSql` unit-tested (bare/WHERE/throws).
- **`CountAsync`/`AnyAsync`** → Task 2 (`AnyAsync` = `Count > 0`).
- **Results are dictionaries** → all row terminals return `IReadOnlyDictionary<string,object?>`.
- **Additive** → `SqlQuery.From`/`ToSql`/`SqlServerExecutor` existing members unchanged (`ToSql` now routes through the `Provider` helper but returns identically).
- **Live demo** → Task 3.
- Type/name consistency: `SqlServerQueryContext.Query`, `SqlQueryProvider.ConnectionString`, `SqlServerExecutor.{ExecuteAsync,ExecuteStreamAsync,ExecuteScalarAsync,CreateCommand}`, `SqlTranslator.{Translate,TranslateCount,BuildModel,Render,RenderCount,AppendWhere}`, extensions `ToSql`/`ToCountSql`/`ToListAsync`/`AsAsyncEnumerable`/`FirstOrDefaultAsync`/`CountAsync`/`AnyAsync` — consistent across tasks.
- **Per-task green:** Task 1 adds only the `ToListAsync` guard test (compiles + passes on its own); Task 2 adds the `CountAsync` guard + `ToCountSql` tests. Each task ends green.
