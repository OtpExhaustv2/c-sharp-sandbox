# Shared Core Library + Specification System via Expression Trees — Design

Date: 2026-06-17
Status: Design under review

## Goal

Two coupled deliverables:

1. **Extract a shared class library `Sandbox.Core`** that holds the reusable building
   blocks currently **duplicated** between the `Sandbox` console project and the
   `sandbox-api` web project (the `Result` functional ecosystem). Both projects, plus a
   new test project, reference this single library instead of copy-pasting.

2. **Add a composable Specification system** built on `System.Linq.Expressions`, living
   in `Sandbox.Core`. A specification encapsulates a query as an
   `Expression<Func<T, bool>>` (criteria) plus optional query-shaping state (ordering,
   paging, projection). Specifications compose via `And` / `Or` / `Not` and evaluate
   against **in-memory `IEnumerable<T>`** (LINQ-to-Objects).

### Decisions locked in

- Library name + root namespace: **`Sandbox.Core`**. **Each concept gets its own folder,
  and the namespace mirrors the folder** (the lib is expected to grow well beyond Result +
  Specifications). Result ecosystem → `Sandbox.Core/Results/` (ns `Sandbox.Core.Results`);
  spec engine → `Sandbox.Core/Specifications/` (ns `Sandbox.Core.Specifications`). Plural
  `Results` avoids clashing with the `Result<,>` type name.
- Specification scope: **predicates + query shaping** (criteria, ordering, paging, projection).
- Evaluation target: **in-memory `IEnumerable<T>`** — no EF Core, no `IQueryable`.
- `DatabaseError` hierarchy **stays in `sandbox-api`** (data-layer error taxonomy, not
  part of the generic core).
- Tests: **MSTest**, in a new `Sandbox.Tests` project.

### Non-goals (YAGNI)

- No `IQueryable` / EF Core / SQL translation. (Parameter-rebinding is deliberately
  translation-friendly so this *could* be added later — but out of scope now.)
- No eager-loading "Includes" — meaningless for in-memory objects.
- No moving `DatabaseError`, controllers' DTOs, or domain models into the core.
- No new functional helpers beyond what already exists; the move is lift-and-shift +
  namespace consolidation, not a redesign of `Result`.

## Current state (the problem)

`Result<T,TError>` + `ResultExtensions`, `ResultHelpers`, `Unit`, and `ApiError` exist as
**byte-identical copies** in two places, differing only by namespace:

| Type | `Sandbox` console | `sandbox-api` |
|------|-------------------|---------------|
| `Result<T,TError>`, `ResultExtensions` | `Sandbox/utils/Result.cs` (ns `Sandbox.utils`) | `sandbox-api/Utils/Result.cs` (ns `sandbox_api.Utils`) |
| `ResultHelpers`, `Unit`, `ApiError` | `Sandbox/utils/ResultHelpers.cs` (ns `Sandbox.Utils`) | `sandbox-api/Utils/ResultHelpers.cs` (ns `sandbox_api.Utils`) |

Note the console project itself is internally inconsistent: `Result` is in namespace
`Sandbox.utils` (lowercase) while `ResultHelpers`/`Unit`/`ApiError` are in `Sandbox.Utils`
(capital), forcing files to import both. The extraction also fixes this by consolidating
on one namespace.

## Architecture

### Project layout (after)

```
Sandbox.slnx
├── Sandbox.Core/            (NEW) class library, net10.0 — one folder per concept
│   ├── Results/                   (ns Sandbox.Core.Results)
│   │   ├── Result.cs                  Result<T,TError> + ResultExtensions
│   │   └── ResultHelpers.cs           ResultHelpers + Unit + ApiError
│   └── Specifications/            (ns Sandbox.Core.Specifications)
│       ├── ExpressionExtensions.cs   ParameterReplacer + And/Or/Not on Expression
│       ├── Specification.cs          Specification<T>, Specification<T,TResult>
│       └── SpecificationEvaluator.cs
├── Sandbox/                 console (Exe)  → references Sandbox.Core
│   └── examples/SpecificationExamples.cs  (NEW) runnable demo
├── sandbox-api/             web API        → references Sandbox.Core
│   └── Specifications/ProductSpecifications.cs  (NEW) concrete Product specs
└── Sandbox.Tests/           (NEW) MSTest    → references Sandbox.Core
```

Folders are concept boundaries; the namespace always mirrors the folder path. As more
concepts land (e.g. `Sandbox.Core/Validation/`, `Sandbox.Core/Pagination/`), each gets its
own folder + matching namespace.

`Sandbox.Core.csproj`: `net10.0`, `ImplicitUsings` enable, `Nullable` enable (match
existing projects). No external package dependencies.

### What moves into `Sandbox.Core`

Into `Sandbox.Core/Results/`, all under namespace **`Sandbox.Core.Results`**:

- `Result<T,TError>` + `ResultExtensions` (lift-and-shift from `Sandbox/utils/Result.cs`).
- `ResultHelpers`, `Unit`, `ApiError` (from `Sandbox/utils/ResultHelpers.cs`).

The duplicate copies in **both** `Sandbox/utils/` and `sandbox-api/Utils/` are **deleted**.

### Migration / blast radius

Repointing is mostly mechanical:

- Add `<ProjectReference>` to `Sandbox.Core` from `Sandbox`, `sandbox-api`, and `Sandbox.Tests`.
- Delete: `Sandbox/utils/Result.cs`, `Sandbox/utils/ResultHelpers.cs`,
  `sandbox-api/Utils/Result.cs`, `sandbox-api/Utils/ResultHelpers.cs`.
- Replace `using Sandbox.utils;` / `using Sandbox.Utils;` / `using sandbox_api.Utils;`
  with `using Sandbox.Core.Results;` across the **12 files** that import them
  (6 in `sandbox-api/Repositories`, 5 in `Sandbox/examples`, plus the moved files themselves).
- Fix fully-qualified references: `Utils.Unit` in the repositories (e.g.
  `Result<Utils.Unit, DatabaseError>`, `Utils.Unit.Value`) → `Unit` with `using Sandbox.Core.Results;`.
- Verify `sandbox-api` controllers and `Program.cs` still resolve `Result`/`Unit`
  (add `using Sandbox.Core.Results;` where needed).
- `DatabaseError` (in `sandbox-api/Models`) is unaffected — it does not depend on the moved
  types, and the repositories keep using `Result<X, DatabaseError>` (core `Result` +
  api `DatabaseError`).

**Gate:** both `Sandbox` and `sandbox-api` build green with zero behavior change before the
Specification engine is added. This is its own plan phase.

### The Expression meat (core of the exercise)

Combining two `Expression<Func<T, bool>>` with AND/OR is **not** body concatenation: each
lambda owns its own `ParameterExpression`, so `Expression.AndAlso(a.Body, b.Body)` yields a
tree where `b.Body` still references `b`'s parameter — invalid when compiled under one
parameter.

Fix: an `ExpressionVisitor` (`ParameterReplacer`) that **rebinds** `b`'s parameter to `a`'s,
then builds `AndAlso` / `OrElse` over the two bodies under a single parameter. `Not` wraps a
single body in `Expression.Not`. This visitor is the centerpiece.

`Expression.Invoke(b, aParam)` would also work in-memory but is the "wrong" technique (does
not translate to SQL, nests invocations). Parameter rebinding is used on purpose — it is the
teachable, translation-friendly approach.

### Specification engine components (namespace `Sandbox.Core.Specifications`)

**`ExpressionExtensions`** — `ParameterReplacer : ExpressionVisitor`; extension methods
`And` / `Or` / `Not` on `Expression<Func<T,bool>>`.

**`Specification<T>`** (abstract):
- State: `Expression<Func<T,bool>>? Criteria` (`null` = match-all);
  ordered list of `(Expression<Func<T,object>> keySelector, bool descending)` for
  `OrderBy` + chained `ThenBy`; `int? Skip`, `int? Take`.
- Protected builders (called from derived constructors): `AddCriteria(...)`,
  `AddOrderBy(...)`, `AddOrderByDescending(...)`, `ApplyPaging(skip, take)`.
- Public: `bool IsSatisfiedBy(T entity)` (compiles criteria, match-all if null);
  `And` / `Or` / `Not` returning a composed spec (internal `AdHocSpecification<T>`
  wrapping the combined criteria via `ExpressionExtensions`).
- Combinator state rule: the **left** spec's ordering/paging carries forward; the right
  operand contributes criteria only. Documented, deliberate simplification (combining
  paging across specs is undefined and avoided).

**`Specification<T,TResult>`** : subclass adding `Expression<Func<T,TResult>> Selector`
(set via `AddSelector(...)`) for projection.

**`SpecificationEvaluator`** (static):
- `IEnumerable<T> Evaluate<T>(IEnumerable<T> source, Specification<T> spec)`:
  filter (`Where` on compiled criteria) → order (`OrderBy`/`OrderByDescending` then
  `ThenBy`/`ThenByDescending`) → page (`Skip` then `Take`).
- `IEnumerable<TResult> Evaluate<T,TResult>(IEnumerable<T> source, Specification<T,TResult> spec)`:
  same pipeline + final `Select(spec.Selector.Compile())`.
- Expressions compiled at evaluation time (no delegate caching — out of scope; noted as a
  possible later optimization).

### Concrete specifications + demonstrations

- **`sandbox-api/Specifications/ProductSpecifications.cs`** — concrete specs over the
  existing `Product` model, proving the engine is shared with the api:
  `AvailableProductSpec` (`p => p.IsAvailable && p.StockQuantity > 0`),
  `PriceBelowSpec(decimal max)`, `InCategorySpec(string category)`.
  Plus a thin, read-only usage: a new `ProductRepository` method
  `GetBySpecificationAsync(Specification<Product> spec)` that loads all products and applies
  `SpecificationEvaluator.Evaluate`. (Read-only, additive — existing methods unchanged.)
- **`Sandbox/examples/SpecificationExamples.cs`** — runnable console demo using a small
  self-contained demo entity (a local `record`), composing specs with `And`/`Or`/`Not`,
  ordering + paging + projection, printing results. `Program.cs` updated to call its `Main()`.

### Data flow

```
build concrete spec(s)
  → compose via And/Or/Not        (criteria trees merged under one parameter)
  → SpecificationEvaluator.Evaluate(source, spec)
       filter (compiled criteria) → order → page [→ project]
  → IEnumerable<T> / IEnumerable<TResult>
  → consumed by api repo method / printed in console example
```

## Error handling

- Null `Criteria` ⇒ match-all (no exception).
- Empty source ⇒ empty result.
- Negative `Skip`/`Take` not specially guarded (LINQ semantics); paging set only via
  `ApplyPaging` with sensible values.
- The `Result` ecosystem move is behavior-preserving; no error-handling changes there.

## Testing — MSTest (`Sandbox.Tests`)

New project: `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`;
`net10.0`; project reference to `Sandbox.Core`. Added to `Sandbox.slnx`.

Test classes (`[TestClass]` / `[TestMethod]`), using a local test entity:
- **ExpressionExtensionsTests** — `And`/`Or`/`Not` truth tables on combined expressions
  (proves parameter-rebinding works under one parameter; the highest-risk logic).
- **SpecificationTests** — `IsSatisfiedBy` true/false; match-all when criteria null;
  `And`/`Or`/`Not` composition results.
- **SpecificationEvaluatorTests** — filtering subset; ordering (asc + `ThenBy`); paging
  (`Skip`/`Take`) window; projection sequence; empty source ⇒ empty.
- **(smoke)** a couple of `Result`/`ResultHelpers` tests to confirm the extracted core
  behaves identically (optional but cheap insurance for the move).

Implementation is **TDD**: write failing tests per unit, then implement.

## Plan phases (sequencing for the implementation plan)

1. **Extract `Sandbox.Core`**: create lib, move Result ecosystem, delete duplicates,
   repoint usings, add project references — both apps build green, zero behavior change.
2. **Spec engine** (TDD): `ExpressionExtensions` → `Specification<T>` → `Specification<T,TResult>`
   → `SpecificationEvaluator`, with `Sandbox.Tests` covering each.
3. **Demonstrate**: api `ProductSpecifications` + read-only repo method; console
   `SpecificationExamples` wired into `Program.cs`.

## Risks

- **Namespace repoint churn** across 12+ files — mechanical but easy to miss a
  fully-qualified `Utils.Unit`; the green-build gate catches it.
- **Referencing an Exe project**: `Sandbox.Tests`/`sandbox-api` reference the *library*, not
  each other's Exe — no issue. The console `Sandbox` stays an Exe referencing the lib.
- Scope is larger than a pure feature (it includes a refactor); phase 1 is isolated and
  verifiable on its own.
