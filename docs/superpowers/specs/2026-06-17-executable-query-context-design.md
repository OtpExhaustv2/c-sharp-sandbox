# Executable Queries via a Query Context — Design

Date: 2026-06-17
Status: Design approved

## Goal

Make the LINQ-to-SQL queries directly executable — the way EF Core's `DbContext` turns an
`IQueryable` into something you can `await foreach` or `await …ToListAsync()` — instead of the
current three-step dance (`build → ToSql() → new SqlServerExecutor(...).ExecuteAsync(...)`).

A `SqlServerQueryContext` owns the connection and hands out connection-bound queries; terminal
operators on those queries translate + execute. The existing `SqlServerExecutor` stays as the
internal engine; callers stop juggling it.

### Target call site

```csharp
var ctx = new SqlServerQueryContext(connectionString);

var rows  = await ctx.Query("Products").Where(r => (decimal)r["Price"] > 50m).ToListAsync();
var first = await ctx.Query("Products").OrderBy(r => r["Price"]).FirstOrDefaultAsync();
var n     = await ctx.Query("Products").Where(r => (bool)r["IsAvailable"] == true).CountAsync();
var any   = await ctx.Query("Products").Where(r => (string)r["Category"] == "Toys").AnyAsync();
await foreach (var row in ctx.Query("Products").AsAsyncEnumerable()) { /* … */ }
```

### Decisions

- **Binding mechanism:** `SqlQueryProvider` gains an **optional** `ConnectionString`.
  `SqlQuery.From(table)` leaves it null (translate-only, unchanged; `ToSql()` works, terminal
  ops throw). `ctx.Query(table)` sets it (executable). Same provider + same builder — reused.
- **Executor stays** as the engine behind the terminal ops; it is no longer part of the call site.
- **Results are dictionaries:** terminal ops return `IReadOnlyDictionary<string,object?>` (the
  builder's `.Select` shapes the SELECT column list; it does not construct projected objects).
- **Aggregates:** `CountAsync`/`AnyAsync` via a new `ToCountSql()` + scalar execution.
- **`CountAsync` throws on `Skip`/`Take`** (counting a paged window needs subquery-wrapping —
  deferred); it ignores projection and ordering (no effect on a count). `AnyAsync` = `Count > 0`.

### Non-goals (YAGNI)

- No count-after-paging (subquery wrap), no EXISTS-based `Any`, no `SingleAsync`/`LastAsync`.
- No joins, group-by, or other aggregates (`Sum`/`Max`).
- No change to `SqlQuery.From` / `ToSql` / `SqlServerExecutor`'s existing members (additive).
- No connection pooling/transaction/retry tuning beyond `SqlConnection` defaults.

## Architecture

### Components — `Sandbox.Core/Sql/`

| File | Change |
|------|--------|
| `SqlQueryProvider.cs` | Add `string? ConnectionString { get; }`; constructor `SqlQueryProvider(string table, string? connectionString = null)`. `CreateQuery<T>` already passes `this`, so derived queries keep the binding. |
| `SqlQueryable.cs` | `SqlQuery.From(string table)` unchanged (provider with null connection). |
| `SqlServerQueryContext.cs` (NEW) | `SqlServerQueryContext(string connectionString)`; `IQueryable<IDictionary<string,object>> Query(string table)` → `new SqlQueryable<…>(new SqlQueryProvider(table, connectionString))`. |
| `SqlServerExecutor.cs` | Add `Task<object?> ExecuteScalarAsync(CompiledSql, CancellationToken)`. Extract a private `static SqlCommand CreateCommand(SqlConnection, CompiledSql)` shared by `ExecuteAsync`/`ExecuteStreamAsync`/`ExecuteScalarAsync` (binds CommandText + parameters). |
| `SqlTranslator.cs` | Add `CompiledSql TranslateCount(Expression, string table)` — build the model via the existing chain-walk, then `RenderCount`. `RenderCount` emits `SELECT COUNT(*) FROM [T] [WHERE <where>]` (reusing WHERE fragments + params); throws `NotSupportedException` if `model.Skip`/`Take` is set. |
| `SqlQueryableExtensions.cs` | Add the terminal operators + `ToCountSql()` (below). |

### Terminal operators (extensions on `IQueryable`, in `SqlQueryableExtensions`)

All resolve the bound provider first; if the provider is not a `SqlQueryProvider` with a
non-null `ConnectionString`, throw `InvalidOperationException("This query is not bound to a
connection. Use SqlServerQueryContext.Query(...), or call ToSql() to inspect the SQL.")`.

- `Task<IReadOnlyList<IReadOnlyDictionary<string,object?>>> ToListAsync(this IQueryable, CancellationToken = default)`
  → `SqlTranslator.Translate` + `SqlServerExecutor.ExecuteAsync`.
- `IAsyncEnumerable<IReadOnlyDictionary<string,object?>> AsAsyncEnumerable(this IQueryable, CancellationToken = default)`
  → `SqlServerExecutor.ExecuteStreamAsync`.
- `Task<IReadOnlyDictionary<string,object?>?> FirstOrDefaultAsync(this IQueryable, CancellationToken = default)`
  → stream; return the first row or `null` (the `await foreach` + early `return` disposes the rest).
- `Task<int> CountAsync(this IQueryable, CancellationToken = default)`
  → `ToCountSql()` + `ExecuteScalarAsync`; `Convert.ToInt32(scalar)`.
- `Task<bool> AnyAsync(this IQueryable, CancellationToken = default)` → `await CountAsync() > 0`.
- `CompiledSql ToCountSql(this IQueryable)` (public, mirrors `ToSql()`) → `SqlTranslator.TranslateCount`.

`SqlQueryProvider`, `SqlTranslator`, and `SqlServerExecutor` are `internal`/in `Sandbox.Core`, so
the extensions (same assembly) can use them directly.

### Data flow

```
ctx.Query("Products")                  IQueryable bound to (table, connectionString)
   .Where(...)                         builds the expression chain (unchanged)
   .ToListAsync()                      → SqlTranslator.Translate(expr, table) → CompiledSql
                                       → new SqlServerExecutor(connStr).ExecuteAsync → rows
   .CountAsync()                       → SqlTranslator.TranslateCount(expr, table) → COUNT CompiledSql
                                       → ExecuteScalarAsync → Convert.ToInt32
```

## Error handling

- Terminal op on an unbound query → `InvalidOperationException` (clear guidance message).
- `CountAsync`/`ToCountSql` with `Skip`/`Take` present → `NotSupportedException`
  ("CountAsync does not support Skip/Take in v1.").
- Underlying `SqlException` surfaces from the awaited terminal op; `await using` in the executor
  disposes connection/command/reader on exception or early break.

## Testing — MSTest

- **Unit (no DB), in `Sandbox.Tests/Sql/`:**
  - `ToCountSql` — bare (`SELECT COUNT(*) FROM [Products]`), with WHERE
    (`SELECT COUNT(*) FROM [Products] WHERE [Price] > @p0`, `@p0=50`), and throws
    `NotSupportedException` when `Skip`/`Take` is present.
  - Unbound-query guard — `SqlQuery.From("X").ToListAsync()` throws `InvalidOperationException`
    (and same for `CountAsync`). No DB needed; the guard fires before any connection.
- **Live (manual), in `SqlServerExamples`:** a context section exercising `ToListAsync`,
  `FirstOrDefaultAsync`, `CountAsync`, `AnyAsync`, and `AsAsyncEnumerable` against
  `DESKTOP-424PEIH\SQLEXPRESS`.

## Plan phases

1. **Binding + context** (TDD where pure): `SqlQueryProvider.ConnectionString`,
   `SqlServerQueryContext`, and the unbound-query guard helper + its test.
2. **Row terminals**: `ToListAsync`, `AsAsyncEnumerable`, `FirstOrDefaultAsync` (executor reuse);
   `ExecuteScalarAsync` + `CreateCommand` DRY in the executor.
3. **Aggregates** (TDD for SQL): `SqlTranslator.TranslateCount` + `ToCountSql` with unit tests;
   `CountAsync`/`AnyAsync`.
4. **Demo**: context section in `SqlServerExamples`; run live.

## Risks

- **Internal access:** terminal-op extensions rely on `SqlQueryProvider`/`SqlTranslator` being in
  the same assembly (`Sandbox.Core`) — they are. No `InternalsVisibleTo` needed for production
  code; `ToCountSql` being public makes the count SQL unit-testable without a DB.
- **`CountAsync` semantics:** ignoring order/projection is correct; throwing on paging is a
  deliberate, documented limitation rather than a silently-wrong count.
