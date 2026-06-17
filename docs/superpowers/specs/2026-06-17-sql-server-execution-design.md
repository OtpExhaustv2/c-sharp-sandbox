# Execute LINQ-Built SQL Against SQL Server — Design

Date: 2026-06-17
Status: Design under review

## Goal

Execute the parameterized T-SQL produced by the LINQ-to-SQL query builder
(`Sandbox.Core/Sql/`, `CompiledSql`) against a real SQL Server instance and materialize the
result rows back into schema-less `IDictionary<string, object>` — closing the loop from
`SqlQuery.From("Products").Where(...).ToSql()` to actual rows, with no POCO models.

This is the execution layer that the query-builder spec deferred. It runs read-only `SELECT`
statements (the only thing the builder generates) plus a one-time idempotent setup of a demo
database.

### Decisions locked in

- **Placement:** the executor + row materializer live in `Sandbox.Core/Sql/` (namespace
  `Sandbox.Core.Sql`); `Sandbox.Core` gains a `Microsoft.Data.SqlClient` package reference.
- **Auth:** Windows integrated security (`Trusted_Connection=True`) — no secret in the
  connection string, so it can be a committed constant.
- **Target instance:** `DESKTOP-424PEIH\SQLEXPRESS`. **Demo DB:** `SandboxLinqSql`
  (created if absent). **Demo table:** `Products`.
- **Result shape:** `IReadOnlyList<IReadOnlyDictionary<string, object?>>` — one dictionary per
  row, column name → value, `DBNull` mapped to `null`.
- **Testing:** the `RowMaterializer` is unit-tested with a fake `IDataReader` (no live DB);
  real end-to-end execution is verified by a runnable console demo (manual).

### Non-goals (YAGNI)

- No writes through the builder (it only emits `SELECT`); the only non-`SELECT` SQL is the
  idempotent demo bootstrap (DDL + seed).
- No connection pooling/retry/transaction management beyond what `SqlConnection` gives by
  default; no async API in v1 (synchronous `Execute`).
- No ORM-style change tracking, no mapping to POCOs, no multi-result-set handling.
- No automated integration tests against a live server (CI has none).

## Architecture

### Components — `Sandbox.Core/Sql/` (ns `Sandbox.Core.Sql`)

| File | Responsibility |
|------|----------------|
| `RowMaterializer.cs` | Pure mapping: `static IReadOnlyList<IReadOnlyDictionary<string, object?>> Materialize(IDataReader reader)`. For each row, for each column `i`: `dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i)`. Takes the `IDataReader` **interface** (not the sealed `SqlDataReader`) so it is unit-testable with a fake. |
| `SqlServerExecutor.cs` | `SqlServerExecutor(string connectionString)`; `IReadOnlyList<IReadOnlyDictionary<string, object?>> Execute(CompiledSql query)`. Opens a `SqlConnection`, creates a `SqlCommand` with `CommandText = query.Sql`, adds each parameter from `query.Parameters` (`command.Parameters.AddWithValue(name, value ?? DBNull.Value)`), runs `ExecuteReader`, returns `RowMaterializer.Materialize(reader)`. |

`Sandbox.Core.csproj` adds `<PackageReference Include="Microsoft.Data.SqlClient" />` (latest
stable for net10.0).

### Demo + setup — console `Sandbox`

| File | Responsibility |
|------|----------------|
| `examples/SqlServerExamples.cs` | `Main()`: (1) bootstrap the demo DB/table idempotently; (2) build several queries with the builder; (3) execute each via `SqlServerExecutor`; (4) print each row dictionary. Wrapped in a try/catch that prints a friendly hint on connection failure. |
| `Program.cs` | Entry point calls `SqlServerExamples.Main()`. |

The bootstrap uses raw `Microsoft.Data.SqlClient` (the executor stays focused on
`CompiledSql` → rows):

- Connect with `Database=master` → `IF DB_ID('SandboxLinqSql') IS NULL CREATE DATABASE [SandboxLinqSql];`
- Connect with `Database=SandboxLinqSql` → create `Products` if absent, then seed deterministically
  (clear + insert, or `MERGE`) so re-runs are idempotent.

`Products` schema:

```sql
CREATE TABLE [Products] (
    [Id]            INT            NOT NULL PRIMARY KEY,
    [Name]          NVARCHAR(100)  NOT NULL,
    [Price]         DECIMAL(18,2)  NOT NULL,
    [Category]      NVARCHAR(50)   NOT NULL,
    [StockQuantity] INT            NOT NULL,
    [IsAvailable]   BIT            NOT NULL
);
```

Seed ~6 rows spanning categories, prices, and availability so the demo queries return
meaningful subsets.

### Connection string

```
Server=DESKTOP-424PEIH\SQLEXPRESS;Database=SandboxLinqSql;Trusted_Connection=True;Encrypt=False
```

- Integrated auth → no secret → committed as a single `const` in the demo (and a `master`
  variant for bootstrap). `Encrypt=False` because Microsoft.Data.SqlClient defaults
  `Encrypt=True` and a local instance has only a self-signed certificate.
- The server name is machine-specific; keeping it in one named constant makes it a one-line
  change for another machine.

### Data flow

```
SqlQuery.From("Products").Where(r => (decimal)r["Price"] > 50m).OrderByDescending(r => r["Price"]).ToSql()
   → CompiledSql { Sql, Parameters }
   → SqlServerExecutor.Execute(compiled)
        SqlCommand(CommandText = Sql); AddWithValue(@pN, value ?? DBNull.Value); ExecuteReader()
   → RowMaterializer.Materialize(reader)
   → IReadOnlyList<IReadOnlyDictionary<string, object?>>
   → demo prints: { Id=4, Name="Bolt", Price=5.00, ... }
```

## Error handling

- `SqlServerExecutor.Execute` opens/disposes the connection (`using`) and lets `SqlException`
  propagate — the caller decides how to react.
- The console demo wraps execution in try/catch, printing a friendly hint on failure
  (instance not running, TCP/named-pipes disabled, DB unreachable) so a first run is diagnosable.
- Parameter values that are `null` are sent as `DBNull.Value`.

## Testing — MSTest (`Sandbox.Tests`)

- **`RowMaterializerTests`** (no live DB): drive `Materialize` with a fake `IDataReader`
  obtained from an in-memory `DataTable.CreateDataReader()`:
  - column names become dictionary keys, in order;
  - `DBNull` cells map to `null`;
  - multiple rows produce multiple dictionaries with correct values;
  - an empty reader yields an empty list.
- **Live execution** is exercised by `SqlServerExamples.Main()` run manually against the
  instance — intentionally not an automated test (CI has no SQL Server). The pure logic that
  *can* be tested without a server (materialization) is covered above.

## Plan phases (for the implementation plan)

1. **Materializer** (TDD): add `Microsoft.Data.SqlClient` to `Sandbox.Core`; implement
   `RowMaterializer` against `IDataReader`; unit-test with `DataTable.CreateDataReader()`.
2. **Executor**: implement `SqlServerExecutor.Execute(CompiledSql)` (connection, command,
   parameter binding, reader → materializer). (No automated test — covered by the demo.)
3. **Demo + bootstrap**: `SqlServerExamples` with idempotent DB/table/seed setup and the
   build→execute→print queries; wire `Program.cs`; run it against `DESKTOP-424PEIH\SQLEXPRESS`
   to confirm real rows come back.

## Risks

- **Connectivity:** SQL Server Express may need TCP/IP or the SQL Browser/named-pipes enabled;
  the friendly catch message calls this out. Integrated auth assumes the running user has a
  login on the instance.
- **`AddWithValue` type inference:** acceptable for the demo's simple types (int, decimal,
  nvarchar, bit); noted as a known simplification rather than explicit `SqlDbType` mapping.
- **Driver dependency on `Sandbox.Core`:** by decision, every Core consumer now references
  `Microsoft.Data.SqlClient`; accepted for sandbox simplicity.
