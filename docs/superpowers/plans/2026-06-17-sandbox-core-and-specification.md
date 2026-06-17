# Sandbox.Core Shared Library + Specification System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the duplicated `Result` ecosystem into a new `Sandbox.Core` class library, then add a composable Specification system (built on `System.Linq.Expressions`) to that library, evaluated in-memory and covered by MSTest.

**Architecture:** New `Sandbox.Core` classlib organized one folder per concept, namespace mirroring folder: `Results/` holds `Result`/`ResultExtensions`/`ResultHelpers`/`Unit` (ns `Sandbox.Core.Results`); `Specifications/` holds the spec engine (ns `Sandbox.Core.Specifications`). The console `Sandbox`, web `sandbox-api`, and a new `Sandbox.Tests` (MSTest) project all reference the library. The spec engine combines `Expression<Func<T,bool>>` predicates via parameter-rebinding (`ExpressionVisitor`) and a `SpecificationEvaluator` applies filter/order/page/project over `IEnumerable<T>`.

**Tech Stack:** C# / .NET 10 (`net10.0`), `System.Linq.Expressions`, MSTest (`Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`), `.slnx` solution format.

**Spec:** `docs/superpowers/specs/2026-06-17-specification-expression-design.md`

---

## File Structure

**New project `Sandbox.Core/`** — one folder per concept; **namespace mirrors folder**:
- `Sandbox.Core.csproj` — classlib, `net10.0`, nullable + implicit usings.
- `Results/Result.cs` — `Result<T,TError>` + `ResultExtensions` (moved, ns `Sandbox.Core.Results`).
- `Results/ResultHelpers.cs` — `ResultHelpers` + `Unit` (moved, ns `Sandbox.Core.Results`). The
  `ApiError` record is NOT moved into Core (app-domain, unused in Core; examples keep their own).
- `Specifications/ExpressionExtensions.cs` — `ParameterReplacer` + `And`/`Or`/`Not` (ns `Sandbox.Core.Specifications`).
- `Specifications/Specification.cs` — `Specification<T>`, `AdHocSpecification<T>`, `Specification<T,TResult>`.
- `Specifications/SpecificationEvaluator.cs` — evaluator.

**New project `Sandbox.Tests/`:**
- `Sandbox.Tests.csproj` — MSTest, references `Sandbox.Core`.
- `TestEntities.cs` — `Widget` record + concrete test specs.
- `ExpressionExtensionsTests.cs`, `SpecificationTests.cs`, `SpecificationEvaluatorTests.cs`, `ResultSmokeTests.cs`.

**Modified `Sandbox/` (console):**
- Delete `utils/Result.cs`, `utils/ResultHelpers.cs`.
- Repoint usings in `examples/*.cs`.
- Add `examples/SpecificationExamples.cs`; update `Program.cs`.

**Modified `sandbox-api/`:**
- Delete `Utils/Result.cs`, `Utils/ResultHelpers.cs`.
- Repoint usings + `Utils.Unit` in `Repositories/*.cs`.
- Add `Specifications/ProductSpecifications.cs`; add one repo method.

**Modified root:** `Sandbox.slnx` (add two projects).

---

## PHASE 1 — Extract `Sandbox.Core` (refactor, behavior-preserving)

### Task 1: Scaffold `Sandbox.Core` + `Sandbox.Tests`, wire solution & references

**Files:**
- Create: `Sandbox.Core/Sandbox.Core.csproj`
- Create: `Sandbox.Tests/Sandbox.Tests.csproj`
- Modify: `Sandbox.slnx`

- [ ] **Step 1: Confirm SDK is .NET 10**

Run: `dotnet --version`
Expected: a `10.` version (e.g. `10.0.100`). If not, stop — the projects target `net10.0`.

- [ ] **Step 2: Create the class library**

Run:
```powershell
dotnet new classlib -n Sandbox.Core -o Sandbox.Core -f net10.0
Remove-Item Sandbox.Core/Class1.cs -Force
```
Expected: `Sandbox.Core/Sandbox.Core.csproj` created.

- [ ] **Step 3: Ensure the library csproj matches repo conventions**

Set `Sandbox.Core/Sandbox.Core.csproj` to exactly:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 4: Create the MSTest project**

Run:
```powershell
dotnet new mstest -n Sandbox.Tests -o Sandbox.Tests -f net10.0
Remove-Item Sandbox.Tests/Test1.cs -Force -ErrorAction SilentlyContinue
Remove-Item Sandbox.Tests/UnitTest1.cs -Force -ErrorAction SilentlyContinue
```
Expected: `Sandbox.Tests/Sandbox.Tests.csproj` created with MSTest package references.

- [ ] **Step 5: Add both projects to the solution**

Run:
```powershell
dotnet sln Sandbox.slnx add Sandbox.Core/Sandbox.Core.csproj Sandbox.Tests/Sandbox.Tests.csproj
```
Expected: `Sandbox.slnx` now lists 4 projects.

- [ ] **Step 6: Add project references**

Run:
```powershell
dotnet add Sandbox/Sandbox.csproj reference Sandbox.Core/Sandbox.Core.csproj
dotnet add sandbox-api/sandbox-api.csproj reference Sandbox.Core/Sandbox.Core.csproj
dotnet add Sandbox.Tests/Sandbox.Tests.csproj reference Sandbox.Core/Sandbox.Core.csproj
```
Expected: three "Reference ... added to the project." messages.

- [ ] **Step 7: Build the empty library to confirm scaffolding**

Run: `dotnet build Sandbox.Core/Sandbox.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```powershell
git add Sandbox.Core Sandbox.Tests Sandbox.slnx Sandbox/Sandbox.csproj sandbox-api/sandbox-api.csproj
git commit -m "build: scaffold Sandbox.Core lib and Sandbox.Tests project"
```

---

### Task 2: Move the `Result` ecosystem into `Sandbox.Core/Results/`

The console copies (`Sandbox/utils/Result.cs`, `Sandbox/utils/ResultHelpers.cs`) become the
single source of truth, moved into `Sandbox.Core/Results/` under namespace
`Sandbox.Core.Results`. The api copies are deleted.

**Files:**
- Create: `Sandbox.Core/Results/Result.cs` (from `Sandbox/utils/Result.cs`)
- Create: `Sandbox.Core/Results/ResultHelpers.cs` (from `Sandbox/utils/ResultHelpers.cs`)
- Delete: `Sandbox/utils/Result.cs`, `Sandbox/utils/ResultHelpers.cs`, `sandbox-api/Utils/Result.cs`, `sandbox-api/Utils/ResultHelpers.cs`

- [ ] **Step 1: Move `Result.cs` into the library**

Move `Sandbox/utils/Result.cs` to `Sandbox.Core/Results/Result.cs`. Change ONLY the namespace
declaration line:
- From: `namespace Sandbox.utils`
- To: `namespace Sandbox.Core.Results`

Leave all type bodies (`Result<T,TError>`, `ResultExtensions`) and the
`using System.Diagnostics.CodeAnalysis;` line unchanged.

- [ ] **Step 2: Move `ResultHelpers.cs` into the library**

Move `Sandbox/utils/ResultHelpers.cs` to `Sandbox.Core/Results/ResultHelpers.cs`. Make these edits:
- Delete the top line `using Sandbox.utils;` (Result now lives in the same namespace).
- Change `namespace Sandbox.Utils` → `namespace Sandbox.Core.Results`.
- Delete the `public record ApiError(...)` declaration at the bottom of the file (app-domain
  type, unused in Core; the console example keeps its own `Sandbox.examples.ApiError`).

Leave `ResultHelpers` and `Unit` bodies unchanged.

- [ ] **Step 3: Delete the duplicate copies**

Run:
```powershell
Remove-Item Sandbox/utils/Result.cs, Sandbox/utils/ResultHelpers.cs, sandbox-api/Utils/Result.cs, sandbox-api/Utils/ResultHelpers.cs -Force
```
Expected: no output, files gone. (`Sandbox/utils/` and `sandbox-api/Utils/` may now be empty —
leave the empty folders; git ignores them.)

- [ ] **Step 4: Build the library**

Run: `dotnet build Sandbox.Core/Sandbox.Core.csproj`
Expected: Build succeeded. (Consumers are not yet repointed — that is Task 3.)

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "refactor: move Result ecosystem into Sandbox.Core, drop duplicates"
```

---

### Task 3: Repoint all consumers to `Sandbox.Core` and restore a green build

**Files (modify):**
- `Sandbox/examples/AdvancedCollectionExamples.cs`
- `Sandbox/examples/CollectionExamples.cs`
- `Sandbox/examples/ThirdPartiesWrappingExamples.cs`
- `Sandbox/examples/ExtensionsExamples.cs`
- `Sandbox/examples/ResultExamples.cs`
- `sandbox-api/Repositories/IProductRepository.cs`
- `sandbox-api/Repositories/ProductRepository.cs`
- `sandbox-api/Repositories/IUserRepository.cs`
- `sandbox-api/Repositories/UserRepository.cs`
- `sandbox-api/Repositories/IOrderRepository.cs`
- `sandbox-api/Repositories/OrderRepository.cs`

- [ ] **Step 1: Repoint the console examples**

In each of `AdvancedCollectionExamples.cs`, `CollectionExamples.cs`,
`ThirdPartiesWrappingExamples.cs`: replace the two top lines
```csharp
using Sandbox.utils;
using Sandbox.Utils;
```
with the single line
```csharp
using Sandbox.Core.Results;
```

In `ExtensionsExamples.cs` and `ResultExamples.cs`: replace the top line
```csharp
using Sandbox.utils;
```
with
```csharp
using Sandbox.Core.Results;
```
(Leave the local `public record Error(...)` in `ResultExamples.cs` untouched.)

- [ ] **Step 2: Repoint the repository interfaces**

In `IProductRepository.cs`, `IUserRepository.cs`, `IOrderRepository.cs`: replace
```csharp
using sandbox_api.Utils;
```
with
```csharp
using Sandbox.Core.Results;
```
Then in `IProductRepository.cs` change the two signatures using `Utils.Unit`:
```csharp
        Task<Result<Unit, DatabaseError>> AdjustStockAsync(int productId, int quantityChange);
        Task<Result<Unit, DatabaseError>> ReserveStockAsync(int productId, int quantity);
```
And in `IUserRepository.cs`:
```csharp
        Task<Result<Unit, DatabaseError>> DeleteUserAsync(int id);
```

- [ ] **Step 3: Repoint the repository implementations**

In `ProductRepository.cs`, `UserRepository.cs`, `OrderRepository.cs`: replace
```csharp
using sandbox_api.Utils;
```
with
```csharp
using Sandbox.Core.Results;
```
Then replace every remaining `Utils.Unit` with `Unit`:
- `ProductRepository.cs`: the method return types on the `AdjustStockAsync` / `ReserveStockAsync` signatures, and `Result<Unit, DatabaseError>.Success(Unit.Value)`.
- `UserRepository.cs`: the `DeleteUserAsync` return type and `Result<Unit, DatabaseError>.Success(Unit.Value)`.

(`OrderRepository.cs` uses `Result` but not `Utils.Unit`; only the `using` swap is needed.)

- [ ] **Step 4: Build the whole solution**

Run: `dotnet build Sandbox.slnx`
Expected: Build succeeded, 0 errors, across `Sandbox.Core`, `Sandbox`, `sandbox-api`, `Sandbox.Tests`.

If any `sandbox-api` file (e.g. a controller or `Program.cs`) reports
`CS0246`/`CS0103` for `Result`, `Unit`, or a `Result` extension method, add
`using Sandbox.Core.Results;` to that file's `using` block and rebuild. (Controllers currently
do not import the utils namespace and rely on `var` + instance methods, so they are
expected to need no change — but the build is the source of truth.)

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "refactor: repoint console and api to Sandbox.Core"
```

---

## PHASE 2 — Specification engine (TDD)

### Task 4: `ExpressionExtensions` — And/Or/Not via parameter rebinding

**Files:**
- Create: `Sandbox.Tests/ExpressionExtensionsTests.cs`
- Create: `Sandbox.Core/Specifications/ExpressionExtensions.cs`

- [ ] **Step 1: Write the failing test**

Create `Sandbox.Tests/ExpressionExtensionsTests.cs`:
```csharp
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Specifications;

namespace Sandbox.Tests
{
    [TestClass]
    public class ExpressionExtensionsTests
    {
        [TestMethod]
        public void And_IsTrueOnlyWhenBothTrue()
        {
            Expression<Func<int, bool>> gt2 = x => x > 2;
            Expression<Func<int, bool>> lt10 = x => x < 10;

            var predicate = gt2.And(lt10).Compile();

            Assert.IsTrue(predicate(5));
            Assert.IsFalse(predicate(1));
            Assert.IsFalse(predicate(20));
        }

        [TestMethod]
        public void Or_IsTrueWhenEitherTrue()
        {
            Expression<Func<int, bool>> lt0 = x => x < 0;
            Expression<Func<int, bool>> gt100 = x => x > 100;

            var predicate = lt0.Or(gt100).Compile();

            Assert.IsTrue(predicate(-5));
            Assert.IsTrue(predicate(200));
            Assert.IsFalse(predicate(50));
        }

        [TestMethod]
        public void Not_InvertsThePredicate()
        {
            Expression<Func<int, bool>> isEven = x => x % 2 == 0;

            var predicate = isEven.Not().Compile();

            Assert.IsFalse(predicate(4));
            Assert.IsTrue(predicate(3));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: FAIL — compile error, `And`/`Or`/`Not` not defined for `Expression<Func<...>>`.

- [ ] **Step 3: Implement `ExpressionExtensions`**

Create `Sandbox.Core/Specifications/ExpressionExtensions.cs`:
```csharp
using System.Linq.Expressions;

namespace Sandbox.Core.Specifications
{
    /// <summary>
    /// Combines predicate expressions by rebinding their parameters onto a single shared
    /// parameter, so the resulting tree is a valid single-parameter lambda (and would
    /// translate to SQL by a query provider, unlike Expression.Invoke).
    /// </summary>
    public static class ExpressionExtensions
    {
        public static Expression<Func<T, bool>> And<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
            => Combine(left, right, Expression.AndAlso);

        public static Expression<Func<T, bool>> Or<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
            => Combine(left, right, Expression.OrElse);

        public static Expression<Func<T, bool>> Not<T>(
            this Expression<Func<T, bool>> expression)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var body = Expression.Not(new ParameterReplacer(parameter).Visit(expression.Body)!);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private static Expression<Func<T, bool>> Combine<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right,
            Func<Expression, Expression, BinaryExpression> merge)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var leftBody = new ParameterReplacer(parameter).Visit(left.Body)!;
            var rightBody = new ParameterReplacer(parameter).Visit(right.Body)!;
            return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
        }

        private sealed class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;

            public ParameterReplacer(ParameterExpression parameter) => _parameter = parameter;

            protected override Expression VisitParameter(ParameterExpression node) => _parameter;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — 3 tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Sandbox.Core/Specifications/ExpressionExtensions.cs Sandbox.Tests/ExpressionExtensionsTests.cs
git commit -m "feat: add Expression And/Or/Not combinators via parameter rebinding"
```

---

### Task 5: `Specification<T>` + composition

**Files:**
- Create: `Sandbox.Tests/TestEntities.cs`
- Create: `Sandbox.Tests/SpecificationTests.cs`
- Create: `Sandbox.Core/Specifications/Specification.cs`

- [ ] **Step 1: Create the shared test entity and concrete test specs**

Create `Sandbox.Tests/TestEntities.cs`:
```csharp
using Sandbox.Core.Specifications;

namespace Sandbox.Tests
{
    public record Widget(int Id, string Name, decimal Price, bool InStock);

    internal sealed class InStockSpec : Specification<Widget>
    {
        public InStockSpec() : base(w => w.InStock) { }
    }

    internal sealed class PriceBelowSpec : Specification<Widget>
    {
        public PriceBelowSpec(decimal max) : base(w => w.Price < max) { }
    }

    internal sealed class CheapestFirstSpec : Specification<Widget>
    {
        public CheapestFirstSpec() => AddOrderBy(w => w.Price);
    }

    internal sealed class PagedByIdSpec : Specification<Widget>
    {
        public PagedByIdSpec(int skip, int take)
        {
            AddOrderBy(w => w.Id);
            ApplyPaging(skip, take);
        }
    }

    internal sealed class WidgetNameSpec : Specification<Widget, string>
    {
        public WidgetNameSpec() => AddSelector(w => w.Name);
    }
}
```

- [ ] **Step 2: Write the failing `Specification<T>` test**

Create `Sandbox.Tests/SpecificationTests.cs`:
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sandbox.Tests
{
    [TestClass]
    public class SpecificationTests
    {
        [TestMethod]
        public void IsSatisfiedBy_ReflectsCriteria()
        {
            var spec = new InStockSpec();

            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(1, "A", 5m, true)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(2, "B", 5m, false)));
        }

        [TestMethod]
        public void And_RequiresBothCriteria()
        {
            var spec = new InStockSpec().And(new PriceBelowSpec(10m));

            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(1, "A", 5m, true)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(2, "B", 50m, true)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(3, "C", 5m, false)));
        }

        [TestMethod]
        public void Or_RequiresEitherCriteria()
        {
            var spec = new InStockSpec().Or(new PriceBelowSpec(10m));

            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(1, "A", 50m, true)));
            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(2, "B", 5m, false)));
            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(3, "C", 50m, false)));
        }

        [TestMethod]
        public void Not_InvertsCriteria()
        {
            var spec = new InStockSpec().Not();

            Assert.IsFalse(spec.IsSatisfiedBy(new Widget(1, "A", 5m, true)));
            Assert.IsTrue(spec.IsSatisfiedBy(new Widget(2, "B", 5m, false)));
        }
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: FAIL — compile error, `Specification<>` not defined.

- [ ] **Step 4: Implement `Specification<T>` (and `Specification<T,TResult>` + `AdHocSpecification<T>`)**

Create `Sandbox.Core/Specifications/Specification.cs`:
```csharp
using System.Linq.Expressions;

namespace Sandbox.Core.Specifications
{
    /// <summary>
    /// A query specification: an optional predicate (null = match all) plus optional
    /// ordering and paging. Composes with And/Or/Not. Evaluate via SpecificationEvaluator.
    /// </summary>
    public abstract class Specification<T>
    {
        public Expression<Func<T, bool>>? Criteria { get; private set; }

        public List<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions { get; }
            = new();

        public int? Skip { get; private set; }
        public int? Take { get; private set; }

        protected Specification() { }

        protected Specification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

        protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

        protected void AddOrderBy(Expression<Func<T, object>> keySelector)
            => OrderExpressions.Add((keySelector, false));

        protected void AddOrderByDescending(Expression<Func<T, object>> keySelector)
            => OrderExpressions.Add((keySelector, true));

        protected void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        public bool IsSatisfiedBy(T entity)
            => Criteria is null || Criteria.Compile()(entity);

        public Specification<T> And(Specification<T> other)
            => WithShapingFrom(this, EffectiveCriteria(this).And(EffectiveCriteria(other)));

        public Specification<T> Or(Specification<T> other)
            => WithShapingFrom(this, EffectiveCriteria(this).Or(EffectiveCriteria(other)));

        public Specification<T> Not()
            => WithShapingFrom(this, EffectiveCriteria(this).Not());

        private static Expression<Func<T, bool>> EffectiveCriteria(Specification<T> spec)
        {
            Expression<Func<T, bool>> matchAll = _ => true;
            return spec.Criteria ?? matchAll;
        }

        // Combinators keep the LEFT spec's ordering/paging; the right operand contributes
        // only its criteria. (Combining paging across specs is undefined, so it is avoided.)
        private static Specification<T> WithShapingFrom(
            Specification<T> source,
            Expression<Func<T, bool>> criteria)
        {
            var spec = new AdHocSpecification<T>(criteria);
            spec.OrderExpressions.AddRange(source.OrderExpressions);
            if (source.Skip.HasValue && source.Take.HasValue)
                spec.ApplyPaging(source.Skip.Value, source.Take.Value);
            return spec;
        }
    }

    /// <summary>Concrete specification produced by And/Or/Not composition.</summary>
    internal sealed class AdHocSpecification<T> : Specification<T>
    {
        public AdHocSpecification(Expression<Func<T, bool>> criteria) : base(criteria) { }
    }

    /// <summary>A specification that also projects matched entities to <typeparamref name="TResult"/>.</summary>
    public abstract class Specification<T, TResult> : Specification<T>
    {
        public Expression<Func<T, TResult>>? Selector { get; private set; }

        protected Specification() { }

        protected Specification(Expression<Func<T, bool>> criteria) : base(criteria) { }

        protected void AddSelector(Expression<Func<T, TResult>> selector) => Selector = selector;
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — `SpecificationTests` (4) + `ExpressionExtensionsTests` (3) all pass.
(`TestEntities.cs` references `AddSelector`/`Specification<T,TResult>`, which now exist.)

- [ ] **Step 6: Commit**

```powershell
git add Sandbox.Core/Specifications/Specification.cs Sandbox.Tests/TestEntities.cs Sandbox.Tests/SpecificationTests.cs
git commit -m "feat: add Specification<T> with And/Or/Not composition"
```

---

### Task 6: `SpecificationEvaluator` — filter, order, page, project

**Files:**
- Create: `Sandbox.Tests/SpecificationEvaluatorTests.cs`
- Create: `Sandbox.Core/Specifications/SpecificationEvaluator.cs`

- [ ] **Step 1: Write the failing evaluator test**

Create `Sandbox.Tests/SpecificationEvaluatorTests.cs`:
```csharp
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Specifications;

namespace Sandbox.Tests
{
    [TestClass]
    public class SpecificationEvaluatorTests
    {
        private static List<Widget> Sample() => new()
        {
            new Widget(1, "Alpha", 30m, true),
            new Widget(2, "Bravo", 10m, false),
            new Widget(3, "Charlie", 20m, true),
            new Widget(4, "Delta", 5m, true),
        };

        [TestMethod]
        public void Filters_BySpecCriteria()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new InStockSpec())
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEquivalent(new[] { 1, 3, 4 }, ids);
        }

        [TestMethod]
        public void Orders_Ascending()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new CheapestFirstSpec())
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEqual(new[] { 4, 2, 3, 1 }, ids);
        }

        [TestMethod]
        public void Pages_WithSkipAndTake()
        {
            var ids = SpecificationEvaluator.Evaluate(Sample(), new PagedByIdSpec(skip: 1, take: 2))
                .Select(w => w.Id).ToList();

            CollectionAssert.AreEqual(new[] { 2, 3 }, ids);
        }

        [TestMethod]
        public void Projects_WithSelector()
        {
            var names = SpecificationEvaluator.Evaluate(Sample(), new WidgetNameSpec()).ToList();

            CollectionAssert.AreEqual(new[] { "Alpha", "Bravo", "Charlie", "Delta" }, names);
        }

        [TestMethod]
        public void EmptySource_ReturnsEmpty()
        {
            var result = SpecificationEvaluator.Evaluate(new List<Widget>(), new InStockSpec()).ToList();

            Assert.AreEqual(0, result.Count);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: FAIL — compile error, `SpecificationEvaluator` not defined.

- [ ] **Step 3: Implement `SpecificationEvaluator`**

Create `Sandbox.Core/Specifications/SpecificationEvaluator.cs`:
```csharp
namespace Sandbox.Core.Specifications
{
    /// <summary>Applies a specification (filter, order, page, project) to an in-memory sequence.</summary>
    public static class SpecificationEvaluator
    {
        public static IEnumerable<T> Evaluate<T>(IEnumerable<T> source, Specification<T> spec)
        {
            var query = source;

            if (spec.Criteria is not null)
                query = query.Where(spec.Criteria.Compile());

            if (spec.OrderExpressions.Count > 0)
            {
                var first = spec.OrderExpressions[0];
                var ordered = first.Descending
                    ? query.OrderByDescending(first.KeySelector.Compile())
                    : query.OrderBy(first.KeySelector.Compile());

                for (var i = 1; i < spec.OrderExpressions.Count; i++)
                {
                    var next = spec.OrderExpressions[i];
                    ordered = next.Descending
                        ? ordered.ThenByDescending(next.KeySelector.Compile())
                        : ordered.ThenBy(next.KeySelector.Compile());
                }

                query = ordered;
            }

            if (spec.Skip.HasValue)
                query = query.Skip(spec.Skip.Value);

            if (spec.Take.HasValue)
                query = query.Take(spec.Take.Value);

            return query;
        }

        public static IEnumerable<TResult> Evaluate<T, TResult>(
            IEnumerable<T> source,
            Specification<T, TResult> spec)
        {
            if (spec.Selector is null)
                throw new InvalidOperationException("A projecting specification requires a Selector.");

            return Evaluate(source, (Specification<T>)spec).Select(spec.Selector.Compile());
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — all evaluator tests pass; full suite green.

- [ ] **Step 5: Commit**

```powershell
git add Sandbox.Core/Specifications/SpecificationEvaluator.cs Sandbox.Tests/SpecificationEvaluatorTests.cs
git commit -m "feat: add SpecificationEvaluator (filter/order/page/project)"
```

---

### Task 7: `Result` move smoke test

**Files:**
- Create: `Sandbox.Tests/ResultSmokeTests.cs`

- [ ] **Step 1: Write the test**

Create `Sandbox.Tests/ResultSmokeTests.cs`:
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Results;

namespace Sandbox.Tests
{
    [TestClass]
    public class ResultSmokeTests
    {
        [TestMethod]
        public void Map_TransformsSuccessValue()
        {
            var result = Result<int, string>.Success(2).Map(x => x * 10);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(20, result.Value);
        }

        [TestMethod]
        public void Combine_ReturnsFirstError()
        {
            var combined = ResultHelpers.Combine(
                Result<int, string>.Success(1),
                Result<int, string>.Failure("boom"));

            Assert.IsTrue(combined.IsFailure);
            Assert.AreEqual("boom", combined.Error);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — confirms the extracted `Sandbox.Core` `Result`/`ResultHelpers` behave correctly.

- [ ] **Step 3: Commit**

```powershell
git add Sandbox.Tests/ResultSmokeTests.cs
git commit -m "test: smoke-test extracted Result ecosystem in Sandbox.Core"
```

---

## PHASE 3 — Demonstrate in api + console

### Task 8: Concrete `Product` specs + read-only repo usage in `sandbox-api`

**Files:**
- Create: `sandbox-api/Specifications/ProductSpecifications.cs`
- Modify: `sandbox-api/Repositories/IProductRepository.cs`
- Modify: `sandbox-api/Repositories/ProductRepository.cs`

- [ ] **Step 1: Create the concrete Product specs**

Create `sandbox-api/Specifications/ProductSpecifications.cs`:
```csharp
using Sandbox.Core.Specifications;
using sandbox_api.Models;

namespace sandbox_api.Specifications
{
    public sealed class AvailableProductSpec : Specification<Product>
    {
        public AvailableProductSpec() : base(p => p.IsAvailable && p.StockQuantity > 0) { }
    }

    public sealed class PriceBelowSpec : Specification<Product>
    {
        public PriceBelowSpec(decimal max) : base(p => p.Price < max) { }
    }

    public sealed class InCategorySpec : Specification<Product>
    {
        public InCategorySpec(string category) : base(p => p.Category == category) { }
    }
}
```

- [ ] **Step 2: Add the spec-based query to the interface**

In `sandbox-api/Repositories/IProductRepository.cs`, add `using Sandbox.Core.Specifications;`
to the using block (it already has `using Sandbox.Core.Results;` and `using sandbox_api.Models;`),
then add this member to the interface:
```csharp
        Task<Result<List<Product>, DatabaseError>> GetBySpecificationAsync(Specification<Product> spec);
```

- [ ] **Step 3: Implement the spec-based query**

In `sandbox-api/Repositories/ProductRepository.cs`, add `using Sandbox.Core.Specifications;`
to the using block, then add this method to the class (e.g. after `GetProductsByCategoryAsync`):
```csharp
        public async Task<Result<List<Product>, DatabaseError>> GetBySpecificationAsync(
            Specification<Product> spec)
        {
            var result = await GetAllProductsAsync();
            return result.Map(products =>
                SpecificationEvaluator.Evaluate(products, spec).ToList());
        }
```

- [ ] **Step 4: Build to verify the api compiles**

Run: `dotnet build sandbox-api/sandbox-api.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```powershell
git add sandbox-api/Specifications/ProductSpecifications.cs sandbox-api/Repositories/IProductRepository.cs sandbox-api/Repositories/ProductRepository.cs
git commit -m "feat(api): add Product specifications and spec-based query"
```

---

### Task 9: Runnable console demo

**Files:**
- Create: `Sandbox/examples/SpecificationExamples.cs`
- Modify: `Sandbox/Program.cs`

- [ ] **Step 1: Create the demo**

Create `Sandbox/examples/SpecificationExamples.cs`:
```csharp
using Sandbox.Core.Specifications;

namespace Sandbox.examples
{
    public class SpecificationExamples
    {
        public record Gadget(int Id, string Name, decimal Price, string Category, bool InStock);

        private sealed class InStockSpec : Specification<Gadget>
        {
            public InStockSpec() : base(g => g.InStock) { }
        }

        private sealed class PriceBelowSpec : Specification<Gadget>
        {
            public PriceBelowSpec(decimal max) : base(g => g.Price < max) { }
        }

        private sealed class CheapInStockSpec : Specification<Gadget>
        {
            public CheapInStockSpec(decimal max)
            {
                AddCriteria(g => g.InStock && g.Price < max);
                AddOrderBy(g => g.Price);
                ApplyPaging(skip: 0, take: 3);
            }
        }

        private sealed class GadgetNameSpec : Specification<Gadget, string>
        {
            public GadgetNameSpec() => AddSelector(g => g.Name);
        }

        public static void Main()
        {
            var gadgets = new List<Gadget>
            {
                new(1, "Widget", 30m, "Tools", true),
                new(2, "Sprocket", 10m, "Tools", false),
                new(3, "Cog", 20m, "Tools", true),
                new(4, "Bolt", 5m, "Hardware", true),
                new(5, "Nut", 2m, "Hardware", true),
            };

            Console.WriteLine("=== IsSatisfiedBy ===");
            var inStock = new InStockSpec();
            Console.WriteLine($"Sprocket in stock? {inStock.IsSatisfiedBy(gadgets[1])}");

            Console.WriteLine("\n=== And: in stock AND under 15 ===");
            var cheapAndAvailable = new InStockSpec().And(new PriceBelowSpec(15m));
            foreach (var g in SpecificationEvaluator.Evaluate(gadgets, cheapAndAvailable))
                Console.WriteLine($"  {g.Name} ({g.Price:C})");

            Console.WriteLine("\n=== Or / Not ===");
            var pricyOrOutOfStock = new PriceBelowSpec(15m).Not().Or(new InStockSpec().Not());
            foreach (var g in SpecificationEvaluator.Evaluate(gadgets, pricyOrOutOfStock))
                Console.WriteLine($"  {g.Name} ({g.Price:C}, inStock={g.InStock})");

            Console.WriteLine("\n=== Ordered + paged (cheapest 3 in-stock under 25) ===");
            foreach (var g in SpecificationEvaluator.Evaluate(gadgets, new CheapInStockSpec(25m)))
                Console.WriteLine($"  {g.Name} ({g.Price:C})");

            Console.WriteLine("\n=== Projection (names only) ===");
            foreach (var name in SpecificationEvaluator.Evaluate(gadgets, new GadgetNameSpec()))
                Console.WriteLine($"  {name}");
        }
    }
}
```

- [ ] **Step 2: Wire the demo into `Program.cs`**

Replace the contents of `Sandbox/Program.cs` with:
```csharp
using Sandbox.examples;

SpecificationExamples.Main();
```
(The previous entry point called `await AdvancedCollectionExamples.Main();`. Switching the
entry point to the new demo is the intended change; the old example class remains in the
project and can be re-enabled by editing this file.)

- [ ] **Step 3: Run the console app to verify output**

Run: `dotnet run --project Sandbox/Sandbox.csproj`
Expected: prints the five sections. Spot-check:
- "And" section lists `Bolt ($5.00)` and `Nut ($2.00)` (in stock, under $15).
- "Ordered + paged" lists three gadgets cheapest-first: `Nut`, `Bolt`, `Cog`.
- "Projection" lists all five names.

- [ ] **Step 4: Full solution build + test**

Run:
```powershell
dotnet build Sandbox.slnx
dotnet test Sandbox.Tests/Sandbox.Tests.csproj
```
Expected: Build succeeded; all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Sandbox/examples/SpecificationExamples.cs Sandbox/Program.cs
git commit -m "feat: add runnable Specification console demo"
```

---

## Self-Review Notes (verification of this plan against the spec)

- **Shared lib extraction** → Tasks 1–3 (scaffold, move, repoint). Duplicate deletion +
  green-build gate covered.
- **Concept folders, namespace mirrors folder** (`Sandbox.Core.Results`, `Sandbox.Core.Specifications`) → Task 2 namespace edits + Task 3 repoint.
- **Spec engine (criteria/order/page/projection)** → Tasks 4–6, each TDD.
- **Parameter-rebinding `ExpressionVisitor`** → Task 4 `ParameterReplacer`.
- **Combinator left-wins shaping rule** → Task 5 `WithShapingFrom`.
- **In-memory `IEnumerable` target** → Task 6 `SpecificationEvaluator`.
- **MSTest project + truth tables/filter/order/page/project/empty** → Tasks 4–7.
- **api sharing (ProductSpecifications + repo method)** → Task 8.
- **Console demo** → Task 9.
- **DatabaseError stays in api** → untouched; repositories keep `Result<X, DatabaseError>`.
- Type/name consistency: `Specification<T>`, `Specification<T,TResult>`,
  `AdHocSpecification<T>`, `SpecificationEvaluator.Evaluate`, `AddCriteria`/`AddOrderBy`/
  `ApplyPaging`/`AddSelector`, `OrderExpressions`/`Criteria`/`Skip`/`Take`/`Selector` are
  used identically across tasks.
