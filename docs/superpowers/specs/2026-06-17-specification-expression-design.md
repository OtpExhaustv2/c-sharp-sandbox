# Specification System via Expression Trees — Design

Date: 2026-06-17
Status: Approved (shape + testing); pending spec review

## Goal

Build a composable **Specification** system for the `Sandbox` console project using
`System.Linq.Expressions`. A specification encapsulates a query as an
`Expression<Func<T, bool>>` (the criteria) plus optional query-shaping state
(ordering, paging, projection). Specifications compose via `And` / `Or` / `Not`
and evaluate against **in-memory `IEnumerable<T>`** collections (LINQ-to-Objects).

Scope chosen: **predicates + query shaping** (criteria, ordering, paging, projection).
Target chosen: **in-memory `IEnumerable<T>`** — no EF Core dependency.

### Non-goals (YAGNI)

- No `IQueryable` / EF Core provider, no SQL translation. (The parameter-rebinding
  approach is deliberately SQL-translatable so this could be added later, but it is
  out of scope now.)
- No eager-loading "Includes" — meaningless for plain in-memory objects.
- No coupling to the existing `Result` type. Specifications stay orthogonal.

## Why Expression and not `Func`

The entire point of the exercise is the expression tree. Combining two
`Expression<Func<T, bool>>` with AND/OR is **not** a body concatenation: each lambda
owns its own `ParameterExpression`, so `Expression.AndAlso(a.Body, b.Body)` produces
a tree where `b.Body` still references `b`'s parameter — invalid when compiled under
`a`'s parameter.

The fix is an `ExpressionVisitor` that **rebinds** `b`'s parameter to `a`'s parameter,
then builds `AndAlso` / `OrElse` over the two bodies under a single parameter. This
`ParameterReplacer` visitor is the centerpiece of the design.

`Expression.Invoke(b, aParam)` would also work in-memory but is the "wrong" way
(it does not translate to SQL and nests invocations). We use parameter rebinding on
purpose because it is the teachable, translatable technique.

## Architecture

### Components

| File | Responsibility |
|------|----------------|
| `Sandbox/utils/Specifications/ExpressionExtensions.cs` | `ParameterReplacer : ExpressionVisitor`; extension methods `And` / `Or` / `Not` on `Expression<Func<T,bool>>`. The core trick. |
| `Sandbox/utils/Specifications/Specification.cs` | Abstract `Specification<T>` (criteria + ordering + paging + combinators + `IsSatisfiedBy`). Also `Specification<T, TResult>` adding a projection `Selector`. |
| `Sandbox/utils/Specifications/SpecificationEvaluator.cs` | Applies a specification to an `IEnumerable<T>`: filter → order → page; projection overload returns `IEnumerable<TResult>`. |
| `Sandbox/examples/SpecificationExamples.cs` | Concrete `Product` specs, composition, ordering/paging/projection, runnable `Main()` printing results. |

Namespace: `Sandbox.utils.Specifications` for the machinery, `Sandbox.examples` for the demo,
matching the existing `Sandbox.utils` / `Sandbox.examples` convention. All machinery types
are `public` so the test project can reference them.

### `Specification<T>` (abstract)

State:
- `Expression<Func<T, bool>>? Criteria` — `null` means "match all".
- `OrderBy` list: ordered list of `(Expression<Func<T, object>> keySelector, bool descending)`
  to support `OrderBy` + chained `ThenBy`.
- `int? Skip`, `int? Take` — paging.

Protected builder methods (called from derived spec constructors):
- `AddCriteria(Expression<Func<T,bool>>)` — sets/initializes the criteria.
- `AddOrderBy(...)`, `AddOrderByDescending(...)`.
- `ApplyPaging(int skip, int take)`.

Public surface:
- `bool IsSatisfiedBy(T entity)` — compiles `Criteria` (match-all if null) and evaluates one entity.
- `Specification<T> And(Specification<T> other)`, `Or(...)`, `Not()` — combine the
  `Criteria` of both specs using `ExpressionExtensions`, returning a new composed spec
  (an internal `AdHocSpecification<T>` wrapping the combined criteria).
  - Combinator semantics for query-shaping state: the **left** spec's ordering/paging
    is carried forward; the right operand contributes only its criteria. (Documented as
    a deliberate, simple rule; combining paging across specs is undefined and avoided.)

### `Specification<T, TResult>`

Subclass of `Specification<T>` adding `Expression<Func<T, TResult>> Selector` set via
`AddSelector(...)`. Used by the evaluator's projection overload.

### `SpecificationEvaluator` (static)

- `IEnumerable<T> Evaluate<T>(IEnumerable<T> source, Specification<T> spec)`:
  1. `Where(spec.Criteria.Compile())` if criteria present.
  2. Apply ordering: first entry → `OrderBy`/`OrderByDescending` (compiled), subsequent →
     `ThenBy`/`ThenByDescending`.
  3. Apply `Skip` then `Take` if set.
- `IEnumerable<TResult> Evaluate<T, TResult>(IEnumerable<T> source, Specification<T, TResult> spec)`:
  same pipeline, final `Select(spec.Selector.Compile())`.

Expressions are compiled at evaluation time. (No caching of compiled delegates — out of
scope for a sandbox demo; noted as a possible later optimization.)

## Data flow

```
build concrete spec(s)
  → compose via And/Or/Not   (criteria trees merged under one parameter)
  → SpecificationEvaluator.Evaluate(products, spec)
      filter (compiled criteria) → order → page [→ project]
  → IEnumerable<T> / IEnumerable<TResult>
  → print in example
```

## Example (`SpecificationExamples.cs`)

Concrete specs over the demo `Product` shape (`Id, Name, Price, StockQuantity, Category, IsAvailable`):
- `AvailableProductSpec` — `p => p.IsAvailable && p.StockQuantity > 0`
- `PriceBelowSpec(decimal max)` — `p => p.Price < max`
- `InCategorySpec(string category)` — `p => p.Category == category`

`Main()` demonstrates:
1. `IsSatisfiedBy` on a single product.
2. `AvailableProductSpec.And(new PriceBelowSpec(50m))` over a list.
3. `Or` and `Not` composition.
4. A spec with ordering + paging.
5. A projecting `Specification<Product, string>` returning product names.

The example uses its own small in-memory `List<Product>` (the example may define a
minimal local `Product` record/class, OR reuse a shared one — see Open question).
`Program.cs` is updated to call `SpecificationExamples.Main()`.

## Error handling

Minimal by design:
- Null `Criteria` ⇒ match-all (no exception).
- Empty source ⇒ empty result.
- Negative `Skip`/`Take` are not specially guarded (LINQ semantics apply); paging is set
  only through `ApplyPaging`, called with sensible values in the demo.

## Testing — MSTest

Add a new test project `Sandbox.Tests` (MSTest: `Microsoft.NET.Test.Sdk`,
`MSTest.TestAdapter`, `MSTest.TestFramework`), target `net10.0`, project reference to
`Sandbox`. Add it to `Sandbox.slnx`.

Test classes (`[TestClass]` / `[TestMethod]`):
- **ExpressionExtensionsTests** — `And` is true only when both true; `Or` true when either;
  `Not` inverts. Verify combined expression evaluates correctly across a truth table
  (proves the parameter-rebinding actually works under one parameter).
- **SpecificationTests** — `IsSatisfiedBy` for a concrete spec (true/false cases);
  match-all when criteria null; `And`/`Or`/`Not` composition results.
- **SpecificationEvaluatorTests** — filtering returns expected subset; ordering
  (asc + `ThenBy`); paging (`Skip`/`Take`) returns expected window; projection returns
  expected `TResult` sequence; empty source ⇒ empty.

Tests drive the implementation (TDD): write failing tests per unit, then implement.

## Open questions for spec review

1. **Shared `Product` type.** The console `Sandbox` project has no `Product` model
   (that lives in `sandbox-api`). Plan: define a small `Product` (record or class) inside
   the example/test scope so the demo is self-contained — agree?
2. **Combinator state rule.** Carrying only the left spec's ordering/paging on
   `And`/`Or` (right contributes criteria only) — acceptable simplification?
