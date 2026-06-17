# LINQ-to-SQL Query Builder (dictionary rows, T-SQL) — Design

Date: 2026-06-17
Status: Design under review

## Goal

Build a small LINQ query provider that translates a LINQ expression tree into a
parameterized **SQL Server (T-SQL)** `SELECT` statement — to understand how EF Core works
"under the hood." The novelty: rows are **schema-less `IDictionary<string, object>`** (no
POCO models), so columns are referenced as `row["ColumnName"]` and the translator maps those
indexer expressions to SQL columns. This mirrors the real-world case of hundreds of tables
with no generated entity classes.

It **generates SQL only** — a SQL string plus a parameter dictionary. It does **not** connect
to or execute against a database, and does not materialize results.

### Decisions locked in

- **Provider style:** a real `IQueryable<IDictionary<string,object>>` + `IQueryProvider`, so
  the standard `System.Linq.Queryable` operators build the expression tree; a `ToSql()` call
  translates it. (Continues the existing `QueryableOrder`/`OrderQueryProvider` experiment in
  `sandbox-api/Models/Order.cs`, but translating instead of compiling-in-memory.)
- **Output:** `CompiledSql` = `string Sql` + `IReadOnlyDictionary<string, object?> Parameters`.
  No execution, no DB dependency.
- **Dialect:** SQL Server / T-SQL — `[bracketed]` identifiers, `@p0` parameters,
  `OFFSET … ROWS FETCH NEXT … ROWS ONLY` paging.
- **Row model:** `IDictionary<string, object>`; columns via `row["Col"]`. No POCO support.
- **Funcletization included:** subtrees that don't reference the row parameter (captured
  locals, `DateTime.Now`, etc.) are evaluated to values and parameterized — exactly how EF
  turns `where x > min` into `WHERE … > @p` with `min` as a parameter.
- **Location:** `Sandbox.Core/Sql/` (namespace `Sandbox.Core.Sql`), per the lib's
  concept-per-folder / namespace-mirrors-folder convention. Tests in `Sandbox.Tests` (MSTest).

### Operator surface (v1)

- `Where` (one or more; multiple are AND-combined).
- `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending`.
- `Skip` / `Take`.
- `Select` projection (column list).
- Inside predicates: comparisons (`== != > >= < <=`), logical (`&& || !`),
  `string.Contains/StartsWith/EndsWith` → `LIKE`, and null checks (`== null` / `!= null`).

### Non-goals (YAGNI)

- No execution, no DB connection, no row materialization.
- No JOIN, GROUP BY, aggregates, DISTINCT, sub-queries, set operations.
- No computed projections (`r["a"] + r["b"]`), no aliasing.
- No POCO entities; no multi-dialect abstraction (T-SQL only — but render is isolated so a
  second dialect could be added later).
- No identifier-escaping for `]` inside names (documented limitation).

## Architecture

### Pipeline

```
SqlQuery.From("Products")                       IQueryable<IDictionary<string,object>>
  .Where(r => (decimal)r["Price"] > min)        System.Linq.Queryable builds a
  .OrderByDescending(r => r["Price"])           MethodCallExpression chain through
  .Skip(10).Take(5)                             provider.CreateQuery(...)
  .Select(r => new { r["Id"], r["Name"] })
  .ToSql()                                       SqlTranslator.Translate(query.Expression)
                                                 → SqlQueryModel → render → CompiledSql
```

### Components — `Sandbox.Core/Sql/` (ns `Sandbox.Core.Sql`)

| File | Responsibility |
|------|----------------|
| `SqlQueryable.cs` | `SqlQueryable<T> : IQueryable<T>` (holds provider + `Expression`). Static entry `SqlQuery.From(string table)` returns `IQueryable<IDictionary<string,object>>` whose root `Expression` is `Expression.Constant(theQueryable)` and whose provider carries the table name. |
| `SqlQueryProvider.cs` | `IQueryProvider`. `CreateQuery<TElement>` wraps the incoming `MethodCallExpression` in a new `SqlQueryable`. `CreateQuery`, `Execute`, `Execute<T>` throw `NotSupportedException("This provider builds SQL only; call ToSql().")`. |
| `Row` (in `SqlQueryable.cs`) | Alias/clarity: the element type is `IDictionary<string, object>`. (No new type; documented for readability.) |
| `SqlQueryModel.cs` | Plain holder the translator fills: `string Table`, `List<string> SelectColumns` (empty = `*`), `List<string> WhereFragments`, `List<(string Column, bool Descending)> OrderBy`, `int? Skip`, `int? Take`, and the collected `List<object?>` parameter values. Renders to T-SQL. |
| `SqlTranslator.cs` | Walks the outer operator chain into a `SqlQueryModel`; delegates predicate/keys to `PredicateTranslator`. |
| `PredicateTranslator.cs` | `ExpressionVisitor`-style translator of a lambda body → SQL boolean fragment, appending parameter values. Owns column detection, funcletization, comparison/logical/LIKE/null rules. |
| `CompiledSql.cs` | `readonly record struct`/class: `string Sql`, `IReadOnlyDictionary<string, object?> Parameters`. |
| `SqlQueryableExtensions.cs` | `CompiledSql ToSql(this IQueryable<IDictionary<string,object>> query)` → `SqlTranslator.Translate(query.Expression)`. |

### How the chain is walked

`query.Expression` is a nested `MethodCallExpression` chain ending at the root
`ConstantExpression` (the `SqlQueryable` carrying the table name). The translator unwraps the
chain to a list (root → outermost), reads the table from the root constant, then folds each
operator call into the `SqlQueryModel`:

- `Where(pred)` → translate the (Quote-unwrapped) lambda body via `PredicateTranslator`, append to `WhereFragments` (multiple `Where` → joined with `AND`).
- `OrderBy`/`OrderByDescending`/`ThenBy`/`ThenByDescending` → translate the key selector body to a single column, append `(Column, Descending)`.
- `Skip(n)` / `Take(n)` → set `Skip`/`Take` (n is funcletized to an int).
- `Select(proj)` → collect every `row["Col"]` reference appearing in the projection body, in order, into `SelectColumns` (handles `new { … }` and `new[]{ … }`).

Unknown method calls in the chain throw `NotSupportedException`.

## Translation rules

- **Column reference:** `row["Price"]` compiles to a call to the `IDictionary<string,object>`
  indexer (`get_Item`) with a string argument. Detection: a `MethodCallExpression` whose
  method is `get_Item` on the row parameter, argument is a constant string (or funcletizable
  to one) → emit `[Price]`. Casts wrap the call in a `Convert`/`ConvertChecked` `UnaryExpression`
  → unwrap and translate the operand (the cast is irrelevant to SQL).
- **Comparisons:** `== != > >= < <=` → `= <> > >= < <=`. Works in either operand order
  (`r["P"] > 50` and `50 < r["P"]`).
- **Logical:** `&&`→`AND`, `||`→`OR`, `!`→`NOT (...)`; binary logical fragments are
  parenthesized: `([Price] > @p0 AND [Category] = @p1)`.
- **Parameters & funcletization:** A subtree that does **not** reference the lambda's row
  parameter is evaluated — `Expression.Lambda(subtree).Compile().DynamicInvoke()` — to a CLR
  value and added as the next `@pN` parameter. This covers literals, captured locals (`min`),
  static reads (`DateTime.Now`), and method results. A subtree that **does** reference the row
  parameter but isn't a recognized column/operator → `NotSupportedException`.
- **LIKE:** `((string)r["Name"]).Contains(s)` → `[Name] LIKE @p0` with value `"%" + s + "%"`;
  `StartsWith(s)` → value `s + "%"`; `EndsWith(s)` → value `"%" + s`. The wildcard lives in the
  parameter value, so the SQL is always `[Col] LIKE @pN`. (`s` is itself funcletized.)
- **NULL:** `r["X"] == null` → `[X] IS NULL`; `r["X"] != null` → `[X] IS NOT NULL`.
- **Projection:** `SelectColumns` = the ordered, de-duplicated `row["Col"]` refs in the
  `Select` body. Empty (no `Select`) renders `SELECT *`. A projection containing anything other
  than column refs (arithmetic, literals, method calls) → `NotSupportedException`.

### Parameter numbering (deterministic, for stable tests)

Parameters are numbered `@p0, @p1, …` in the order the values are appended **during render**,
which is: WHERE fragments left-to-right, then `Skip` (OFFSET), then `Take` (FETCH).
`SelectColumns` and `ORDER BY` contribute no parameters.

### Rendering (T-SQL)

```
SELECT {*|[c1], [c2], …}
FROM [Table]
[WHERE <fragment>]
[ORDER BY [c] [DESC], …]
[OFFSET @pN ROWS [FETCH NEXT @pM ROWS ONLY]]
```

- **Paging needs ORDER BY in T-SQL.** If `Skip`/`Take` is present but no `OrderBy`, emit
  `ORDER BY (SELECT 1)` (the exact trick EF Core uses).
- `Take` without `Skip` → `OFFSET @p{skip=0} ROWS FETCH NEXT @p ROWS ONLY` (FETCH requires
  OFFSET in T-SQL; offset defaults to 0, still parameterized).
- `Skip` without `Take` → `OFFSET @p ROWS` (no FETCH).

## Error handling

- Any unsupported node (unknown operator method, non-column member access on the row,
  computed projection, unsupported method call) → `NotSupportedException` naming the node —
  the way a real LINQ provider reports an untranslatable query.
- `ToSql()` on a query with no operators → `SELECT * FROM [Table]`.

## Testing — MSTest (`Sandbox.Tests`)

Assert the generated `Sql` string and `Parameters` for representative queries (one behavior
per test):

- **Bare table** → `SELECT * FROM [Products]`, no params.
- **Single comparison** `r => (decimal)r["Price"] > 50m` → `SELECT * FROM [Products] WHERE [Price] > @p0`, `{@p0: 50}`.
- **Operand order** `50m < r["Price"]` → same as above.
- **AND / OR / NOT** → parenthesized fragments, correct params.
- **Funcletized local** `var min = 50; …> min` → `> @p0` with `{@p0: 50}` (proves funcletization).
- **LIKE trio** Contains/StartsWith/EndsWith → `[Name] LIKE @p0` with `%x%` / `x%` / `%x`.
- **NULL** `== null` / `!= null` → `IS NULL` / `IS NOT NULL`, no param.
- **ORDER BY** asc + `ThenByDescending` → `ORDER BY [a], [b] DESC`.
- **Paging** `OrderBy(...).Skip(10).Take(5)` → `… ORDER BY [Price] OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY`, `{@p0:10, @p1:5}`.
- **Paging without order** `Skip(10).Take(5)` → `ORDER BY (SELECT 1) OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY`.
- **Take only** → `OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY` with `{@p0:0, @p1:n}`.
- **Projection** `Select(r => new { r["Id"], r["Name"] })` → `SELECT [Id], [Name] FROM …`.
- **Composed** WHERE + ORDER BY + paging + projection together (parameter numbering stable).
- **Unsupported** computed projection / unknown method → `NotSupportedException`.

TDD: each rule is a failing test first, then the minimal translator code to pass it.

## Plan phases (for the implementation plan)

1. **Provider plumbing** (TDD): `SqlQueryable<T>`, `SqlQuery.From`, `SqlQueryProvider`,
   `CompiledSql`, `ToSql()` — a bare `SELECT * FROM [Table]` round-trips through real LINQ.
2. **Predicate translation** (TDD): `PredicateTranslator` — columns, casts, comparisons,
   logical, funcletization/parameters, LIKE, NULL. `Where` wired into the model.
3. **Shaping** (TDD): `OrderBy`/`ThenBy`, `Skip`/`Take` (OFFSET/FETCH + the ORDER BY rules),
   `Select` projection. Composed-query test.

## Risks

- **Funcletization correctness:** the "references the row parameter?" check must be reliable;
  a parameter-detection visitor drives it. Tested directly via the captured-local case.
- **Provider plumbing subtlety:** `System.Linq.Queryable` requires `CreateQuery<T>` to return
  the right element type and preserve the expression; getting the generic glue right is the
  fiddly part of phase 1 (the existing `Order.cs` experiment is a working reference).
- Scope is contained to a single-table SELECT; phases are independently testable.
