# Streaming Execute for SQL Server — Design

Date: 2026-06-17
Status: Design approved

## Goal

Add a streaming counterpart to `SqlServerExecutor.ExecuteAsync` that yields rows one at a time
as an `IAsyncEnumerable<IReadOnlyDictionary<string, object?>>`, instead of buffering the whole
result set into a list. Useful for large result sets and early-exit consumers.

## Decisions

- **Keep both**: buffered `ExecuteAsync` (existing) and new streaming `ExecuteStreamAsync`.
- **Share the per-row mapping**: extract `RowMaterializer.MapRow(DbDataReader)` and use it from
  both the buffered and streaming paths (DRY; identical column→value, `DBNull`→`null` behavior).
- **Async iterator** with `[EnumeratorCancellation]` so the caller's token flows through
  `await foreach (... .WithCancellation(ct))`.

### Non-goals (YAGNI)

- No change to the SQL generation, the builder, or `ExecuteAsync`'s contract.
- No batching/chunking API; no `IDataReader`-level exposure to callers.

## Components — `Sandbox.Core/Sql/`

| Member | Change |
|--------|--------|
| `RowMaterializer.MapRow(DbDataReader reader)` | NEW `public static IReadOnlyDictionary<string, object?>` — maps the reader's **current** row (`GetName(i)` → `IsDBNull(i) ? null : GetValue(i)`). |
| `RowMaterializer.MaterializeAsync(...)` | unchanged behavior; its loop body now calls `MapRow(reader)` (refactor only). |
| `SqlServerExecutor.ExecuteStreamAsync(CompiledSql query, [EnumeratorCancellation] CancellationToken ct = default)` | NEW async iterator returning `IAsyncEnumerable<IReadOnlyDictionary<string, object?>>`. |

`ExecuteStreamAsync` body:
```
await using var connection = new SqlConnection(_connectionString);
await connection.OpenAsync(ct);
await using var command = connection.CreateCommand();
command.CommandText = query.Sql;
foreach (var (name, value) in query.Parameters)
    command.Parameters.AddWithValue(name, value ?? DBNull.Value);
await using var reader = await command.ExecuteReaderAsync(ct);
while (await reader.ReadAsync(ct))
    yield return RowMaterializer.MapRow(reader);
```
The `await using` resources stay open for the lifetime of enumeration and are disposed when the
consumer completes or breaks early — nothing is buffered.

## Data flow

```
…ToSql() → CompiledSql
   → await foreach (var row in executor.ExecuteStreamAsync(sql).WithCancellation(ct))
        each MoveNextAsync → reader.ReadAsync → MapRow(reader) → one dictionary
```

## Error handling

- Same as buffered: `SqlException` surfaces from the iterator (on first `MoveNextAsync`);
  `await using` disposes the connection/command/reader on exception or early `break`.

## Testing — MSTest

- **Unit (`RowMaterializerTests`):** a `MapRow_MapsCurrentRow` test — advance a `DataTableReader`
  one row, call `MapRow`, assert column→value keying and `DBNull`→`null`. No live DB.
- **Live:** add a streaming `await foreach` block to `SqlServerExamples` and run it against the
  instance (manual, consistent with how `ExecuteAsync` is verified).

## Plan phases

1. **`MapRow` extraction** (TDD): add `RowMaterializer.MapRow`, refactor `MaterializeAsync` to use
   it, add the `MapRow` unit test — existing materializer tests stay green.
2. **`ExecuteStreamAsync`**: add the async iterator; build green (no automated test — live path).
3. **Demo**: add a streaming section to `SqlServerExamples`; run it against `DESKTOP-424PEIH\SQLEXPRESS`.
