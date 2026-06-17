# LINQ-to-SQL Query Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A LINQ query provider over `IDictionary<string,object>` rows that translates a LINQ expression tree into a parameterized SQL Server `SELECT` string + parameter dictionary (no execution).

**Architecture:** A real `IOrderedQueryable<IDictionary<string,object>>` + `IQueryProvider` lets `System.Linq.Queryable` operators build an expression tree; `ToSql()` hands that tree to `SqlTranslator`, which walks the operator chain into a `SqlQueryModel` and renders T-SQL. A `PredicateTranslator` turns lambda bodies into SQL fragments, mapping `row["Col"]` indexer expressions to `[Col]` and funcletizing non-row subtrees into `@pN` parameters.

**Tech Stack:** C# / .NET 10, `System.Linq.Expressions`, `System.Linq.Queryable`, MSTest.

**Spec:** `docs/superpowers/specs/2026-06-17-linq-to-sql-query-builder-design.md`

---

## File Structure

All under `Sandbox.Core/Sql/`, namespace `Sandbox.Core.Sql` (concept-per-folder, namespace mirrors folder):

- `SqlQueryable.cs` — `SqlQueryable<T> : IOrderedQueryable<T>` + static `SqlQuery.From(string)`.
- `SqlQueryProvider.cs` — `IQueryProvider` (builds the chain; never executes).
- `CompiledSql.cs` — result: `Sql` + `Parameters`.
- `SqlQueryableExtensions.cs` — `ToSql(this IQueryable)`.
- `SqlQueryModel.cs` — plain holder the translator fills.
- `SqlTranslator.cs` — chain walk + render + the `SqlParameterBag` helper.
- `PredicateTranslator.cs` — lambda-body → SQL fragment; columns, casts, comparisons, logical, LIKE, NULL, funcletization, projection/key extraction.

Tests in `Sandbox.Tests/`:
- `SqlQueryBuilderTests.cs` — all behavior tests (one method per behavior).

`Sandbox.Tests` already references `Sandbox.Core`. No new projects or packages.

---

## Task 1: Provider plumbing + bare `SELECT * FROM [Table]`

**Files:**
- Create: `Sandbox.Core/Sql/SqlQueryable.cs`
- Create: `Sandbox.Core/Sql/SqlQueryProvider.cs`
- Create: `Sandbox.Core/Sql/CompiledSql.cs`
- Create: `Sandbox.Core/Sql/SqlQueryableExtensions.cs`
- Create: `Sandbox.Core/Sql/SqlQueryModel.cs`
- Create: `Sandbox.Core/Sql/SqlTranslator.cs`
- Create: `Sandbox.Tests/SqlQueryBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Sandbox.Tests/SqlQueryBuilderTests.cs`:
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox.Core.Sql;

namespace Sandbox.Tests
{
    [TestClass]
    public class SqlQueryBuilderTests
    {
        [TestMethod]
        public void BareTable_SelectsStar()
        {
            var compiled = SqlQuery.From("Products").ToSql();

            Assert.AreEqual("SELECT * FROM [Products]", compiled.Sql);
            Assert.AreEqual(0, compiled.Parameters.Count);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~BareTable_SelectsStar"`
Expected: FAIL — `SqlQuery`/`ToSql` not defined (compile error).

- [ ] **Step 3: Create `CompiledSql.cs`**

```csharp
namespace Sandbox.Core.Sql
{
    /// <summary>The result of translating a query: a SQL string and its parameter values.</summary>
    public sealed class CompiledSql
    {
        public string Sql { get; }
        public IReadOnlyDictionary<string, object?> Parameters { get; }

        public CompiledSql(string sql, IReadOnlyDictionary<string, object?> parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }

        public override string ToString() => Sql;
    }
}
```

- [ ] **Step 4: Create `SqlQueryable.cs`**

```csharp
using System.Collections;
using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// An IQueryable whose provider builds SQL instead of executing. Implements
    /// IOrderedQueryable so Queryable.OrderBy/ThenBy can chain (their internal cast to
    /// IOrderedQueryable&lt;T&gt; would otherwise fail).
    /// </summary>
    public sealed class SqlQueryable<T> : IOrderedQueryable<T>
    {
        public SqlQueryable(IQueryProvider provider, Expression? expression = null)
        {
            Provider = provider;
            Expression = expression ?? Expression.Constant(this);
        }

        public Type ElementType => typeof(T);
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
            => throw new NotSupportedException("This query builds SQL only; call ToSql().");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Entry point: start a query against a named table.</summary>
    public static class SqlQuery
    {
        public static IQueryable<IDictionary<string, object>> From(string table)
            => new SqlQueryable<IDictionary<string, object>>(new SqlQueryProvider(table));
    }
}
```

- [ ] **Step 5: Create `SqlQueryProvider.cs`**

```csharp
using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Builds the LINQ expression chain (via CreateQuery) but never executes — the query is
    /// turned into SQL by ToSql(). Carries the root table name.
    /// </summary>
    internal sealed class SqlQueryProvider : IQueryProvider
    {
        public string Table { get; }

        public SqlQueryProvider(string table) => Table = table;

        public IQueryable CreateQuery(Expression expression)
            => new SqlQueryable<IDictionary<string, object>>(this, expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new SqlQueryable<TElement>(this, expression);

        public object Execute(Expression expression)
            => throw new NotSupportedException("This provider builds SQL only; call ToSql().");

        public TResult Execute<TResult>(Expression expression)
            => throw new NotSupportedException("This provider builds SQL only; call ToSql().");
    }
}
```

- [ ] **Step 6: Create `SqlQueryableExtensions.cs`**

```csharp
namespace Sandbox.Core.Sql
{
    public static class SqlQueryableExtensions
    {
        /// <summary>Translate the built query into a parameterized SQL Server SELECT.</summary>
        public static CompiledSql ToSql(this IQueryable query)
        {
            if (query.Provider is not SqlQueryProvider provider)
                throw new NotSupportedException("ToSql requires a query created via SqlQuery.From.");

            return SqlTranslator.Translate(query.Expression, provider.Table);
        }
    }
}
```

- [ ] **Step 7: Create `SqlQueryModel.cs`**

```csharp
namespace Sandbox.Core.Sql
{
    /// <summary>Mutable holder the translator fills while walking the operator chain.</summary>
    internal sealed class SqlQueryModel
    {
        public string Table { get; set; } = "";
        public List<string> SelectColumns { get; } = new();   // empty => SELECT *
        public List<string> WhereFragments { get; } = new();   // joined with AND
        public List<(string Column, bool Descending)> OrderBy { get; } = new();
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }
}
```

- [ ] **Step 8: Create `SqlTranslator.cs`**

```csharp
using System.Linq.Expressions;
using System.Text;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Walks the LINQ operator chain (Where/OrderBy/Skip/Take/Select) into a SqlQueryModel and
    /// renders SQL Server T-SQL. Parameters are numbered @p0.. in render order: WHERE, then
    /// OFFSET (Skip), then FETCH (Take).
    /// </summary>
    internal static class SqlTranslator
    {
        public static CompiledSql Translate(Expression expression, string table)
        {
            var model = new SqlQueryModel { Table = table };
            var parameters = new SqlParameterBag();

            // Unwrap the method-call chain. Pushing outer-first makes the stack pop
            // innermost-first, i.e. in the order the user wrote the operators.
            var calls = new Stack<MethodCallExpression>();
            var current = expression;
            while (current is MethodCallExpression call)
            {
                calls.Push(call);
                current = call.Arguments[0]; // Queryable operators take the source as arg 0
            }

            foreach (var call in calls)
                Apply(call, model, parameters);

            return new CompiledSql(Render(model, parameters), parameters.ToDictionary());
        }

        private static void Apply(MethodCallExpression call, SqlQueryModel model, SqlParameterBag parameters)
        {
            switch (call.Method.Name)
            {
                default:
                    throw new NotSupportedException($"Unsupported query operator: {call.Method.Name}");
            }
        }

        private static string Render(SqlQueryModel m, SqlParameterBag parameters)
        {
            var sb = new StringBuilder();

            sb.Append("SELECT ");
            sb.Append(m.SelectColumns.Count == 0 ? "*" : string.Join(", ", m.SelectColumns));
            sb.Append(" FROM [").Append(m.Table).Append(']');

            if (m.WhereFragments.Count > 0)
                sb.Append(" WHERE ").Append(string.Join(" AND ", m.WhereFragments));

            var hasPaging = m.Skip.HasValue || m.Take.HasValue;

            if (m.OrderBy.Count > 0)
            {
                sb.Append(" ORDER BY ");
                sb.Append(string.Join(", ",
                    m.OrderBy.Select(o => o.Descending ? $"{o.Column} DESC" : o.Column)));
            }
            else if (hasPaging)
            {
                // T-SQL requires ORDER BY for OFFSET/FETCH; this is the trick EF Core uses.
                sb.Append(" ORDER BY (SELECT 1)");
            }

            if (hasPaging)
            {
                sb.Append(" OFFSET ").Append(parameters.Add(m.Skip ?? 0)).Append(" ROWS");
                if (m.Take.HasValue)
                    sb.Append(" FETCH NEXT ").Append(parameters.Add(m.Take.Value)).Append(" ROWS ONLY");
            }

            return sb.ToString();
        }
    }

    /// <summary>Collects parameter values and hands out @p0, @p1, … names in creation order.</summary>
    internal sealed class SqlParameterBag
    {
        private readonly Dictionary<string, object?> _parameters = new();

        public string Add(object? value)
        {
            var name = $"@p{_parameters.Count}";
            _parameters[name] = value;
            return name;
        }

        public IReadOnlyDictionary<string, object?> ToDictionary() => _parameters;
    }
}
```

- [ ] **Step 9: Run test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~BareTable_SelectsStar"`
Expected: PASS.

- [ ] **Step 10: Commit**

```powershell
git add Sandbox.Core/Sql Sandbox.Tests/SqlQueryBuilderTests.cs
git commit -m "feat(sql): query provider plumbing + bare SELECT translation"
```

---

## Task 2: Predicate translation — `Where`, columns, comparisons, logical, LIKE, NULL, funcletization

**Files:**
- Create: `Sandbox.Core/Sql/PredicateTranslator.cs`
- Modify: `Sandbox.Core/Sql/SqlTranslator.cs` (add `Where` case + `GetLambda` helper)
- Modify: `Sandbox.Tests/SqlQueryBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these methods inside `SqlQueryBuilderTests`:
```csharp
        [TestMethod]
        public void Where_Comparison_EmitsColumnOpParam()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_ValueOnLeft_FlipsOperator()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => 50m < (decimal)r["Price"])
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_CapturedLocal_IsFuncletizedToParam()
        {
            var min = 50m;
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > min)
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] WHERE [Price] > @p0", compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_AndOrNot_AreParenthesized()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => ((decimal)r["Price"] > 50m && (string)r["Category"] == "Tools")
                            || !((bool)r["IsAvailable"] == true))
                .ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] WHERE (([Price] > @p0 AND [Category] = @p1) OR NOT ([IsAvailable] = @p2))",
                compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
            Assert.AreEqual("Tools", compiled.Parameters["@p1"]);
            Assert.AreEqual(true, compiled.Parameters["@p2"]);
        }

        [TestMethod]
        public void Where_StringMethods_TranslateToLike()
        {
            var contains = SqlQuery.From("Products").Where(r => ((string)r["Name"]).Contains("lap")).ToSql();
            Assert.AreEqual("SELECT * FROM [Products] WHERE [Name] LIKE @p0", contains.Sql);
            Assert.AreEqual("%lap%", contains.Parameters["@p0"]);

            var starts = SqlQuery.From("Products").Where(r => ((string)r["Name"]).StartsWith("lap")).ToSql();
            Assert.AreEqual("lap%", starts.Parameters["@p0"]);

            var ends = SqlQuery.From("Products").Where(r => ((string)r["Name"]).EndsWith("top")).ToSql();
            Assert.AreEqual("%top", ends.Parameters["@p0"]);
        }

        [TestMethod]
        public void Where_NullChecks_TranslateToIsNull()
        {
            var isNull = SqlQuery.From("Orders").Where(r => r["CompletedAt"] == null).ToSql();
            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NULL", isNull.Sql);
            Assert.AreEqual(0, isNull.Parameters.Count);

            var isNotNull = SqlQuery.From("Orders").Where(r => r["CompletedAt"] != null).ToSql();
            Assert.AreEqual("SELECT * FROM [Orders] WHERE [CompletedAt] IS NOT NULL", isNotNull.Sql);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~Where_"`
Expected: FAIL — `Where` is an unsupported operator (NotSupportedException) / compile is fine but translation throws.

- [ ] **Step 3: Create `PredicateTranslator.cs`**

```csharp
using System.Linq.Expressions;

namespace Sandbox.Core.Sql
{
    /// <summary>
    /// Translates a predicate/key/projection lambda body into SQL. Maps row["Col"] indexer
    /// calls to [Col]; evaluates ("funcletizes") any subtree that does not reference the row
    /// parameter into a parameter value.
    /// </summary>
    internal sealed class PredicateTranslator
    {
        private readonly ParameterExpression _row;
        private readonly SqlParameterBag _parameters;

        private PredicateTranslator(ParameterExpression row, SqlParameterBag parameters)
        {
            _row = row;
            _parameters = parameters;
        }

        /// <summary>Translate a boolean predicate body to a SQL WHERE fragment.</summary>
        public static string Translate(LambdaExpression lambda, SqlParameterBag parameters)
            => new PredicateTranslator(lambda.Parameters[0], parameters).VisitBool(lambda.Body);

        /// <summary>Extract a single column from an order-by key selector.</summary>
        public static string Column(LambdaExpression lambda)
        {
            var t = new PredicateTranslator(lambda.Parameters[0], null!);
            return t.AsColumn(lambda.Body)
                ?? throw new NotSupportedException("Order-by key must be a row[\"Col\"] reference.");
        }

        /// <summary>Extract the ordered, distinct column list from a Select projection body.</summary>
        public static IReadOnlyList<string> ProjectionColumns(LambdaExpression lambda)
        {
            var t = new PredicateTranslator(lambda.Parameters[0], null!);
            var columns = new List<string>();
            t.CollectColumns(lambda.Body, columns);
            if (columns.Count == 0)
                throw new NotSupportedException("Projection must reference at least one column.");
            return columns;
        }

        private string VisitBool(Expression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                    var and = (BinaryExpression)node;
                    return $"({VisitBool(and.Left)} AND {VisitBool(and.Right)})";

                case ExpressionType.OrElse:
                    var or = (BinaryExpression)node;
                    return $"({VisitBool(or.Left)} OR {VisitBool(or.Right)})";

                case ExpressionType.Not:
                    return $"NOT ({VisitBool(((UnaryExpression)node).Operand)})";

                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                    return VisitComparison((BinaryExpression)node);

                case ExpressionType.Call:
                    return VisitLike((MethodCallExpression)node);

                default:
                    throw new NotSupportedException($"Unsupported predicate expression: {node.NodeType}");
            }
        }

        private string VisitComparison(BinaryExpression b)
        {
            if (IsNull(b.Right))
                return $"{RequireColumn(b.Left)} {(b.NodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL")}";
            if (IsNull(b.Left))
                return $"{RequireColumn(b.Right)} {(b.NodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL")}";

            var leftColumn = AsColumn(b.Left);
            var rightColumn = AsColumn(b.Right);
            var op = SqlOperator(b.NodeType);

            if (leftColumn != null && rightColumn == null)
                return $"{leftColumn} {op} {_parameters.Add(Evaluate(b.Right))}";
            if (rightColumn != null && leftColumn == null)
                return $"{rightColumn} {Flip(op)} {_parameters.Add(Evaluate(b.Left))}";

            throw new NotSupportedException("A comparison must be between one column and one value.");
        }

        private string VisitLike(MethodCallExpression call)
        {
            if (call.Object != null && call.Method.Name is "Contains" or "StartsWith" or "EndsWith")
            {
                var column = AsColumn(call.Object)
                    ?? throw new NotSupportedException($"{call.Method.Name} must be called on a column.");
                var argument = Evaluate(call.Arguments[0]);
                var pattern = call.Method.Name switch
                {
                    "Contains" => $"%{argument}%",
                    "StartsWith" => $"{argument}%",
                    _ => $"%{argument}",
                };
                return $"{column} LIKE {_parameters.Add(pattern)}";
            }

            throw new NotSupportedException($"Unsupported method in predicate: {call.Method.Name}");
        }

        // row["Col"] (optionally wrapped in a cast) -> "[Col]"; otherwise null.
        private string? AsColumn(Expression node)
        {
            node = Unwrap(node);
            if (node is MethodCallExpression call
                && call.Method.Name == "get_Item"
                && call.Object != null
                && ReferencesRow(call.Object)
                && call.Arguments.Count == 1)
            {
                var name = (string)Evaluate(call.Arguments[0])!;
                return $"[{name}]";
            }
            return null;
        }

        private void CollectColumns(Expression node, List<string> columns)
        {
            switch (node)
            {
                case NewExpression anonymous:            // new { Id = r["Id"], Name = r["Name"] }
                    foreach (var argument in anonymous.Arguments) CollectColumns(argument, columns);
                    break;
                case NewArrayExpression array:           // new[] { r["Id"], r["Name"] }
                    foreach (var element in array.Expressions) CollectColumns(element, columns);
                    break;
                default:
                    var column = AsColumn(node)
                        ?? throw new NotSupportedException("A projection may only contain column references.");
                    if (!columns.Contains(column)) columns.Add(column);
                    break;
            }
        }

        // Evaluate a subtree that does not depend on the row, turning it into a CLR value.
        private object? Evaluate(Expression node)
        {
            if (ReferencesRow(node))
                throw new NotSupportedException($"Cannot translate expression that references the row: {node}");
            return Expression.Lambda(node).Compile().DynamicInvoke();
        }

        private bool ReferencesRow(Expression node) => new RowReferenceFinder(_row).IsFound(node);

        private static Expression Unwrap(Expression node)
            => node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u
                ? Unwrap(u.Operand)
                : node;

        private string RequireColumn(Expression node)
            => AsColumn(node) ?? throw new NotSupportedException("Expected a row[\"Col\"] reference.");

        private static bool IsNull(Expression node)
            => node is ConstantExpression { Value: null };

        private static string SqlOperator(ExpressionType type) => type switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Unsupported operator: {type}"),
        };

        private static string Flip(string op) => op switch
        {
            ">" => "<",
            ">=" => "<=",
            "<" => ">",
            "<=" => ">=",
            _ => op, // = and <> are symmetric
        };

        private sealed class RowReferenceFinder : ExpressionVisitor
        {
            private readonly ParameterExpression _row;
            private bool _found;

            public RowReferenceFinder(ParameterExpression row) => _row = row;

            public bool IsFound(Expression node)
            {
                _found = false;
                Visit(node);
                return _found;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _row) _found = true;
                return node;
            }
        }
    }
}
```

- [ ] **Step 4: Wire `Where` into `SqlTranslator`**

In `Sandbox.Core/Sql/SqlTranslator.cs`, add the `Where` case to the `Apply` switch (above the `default`):
```csharp
                case "Where":
                    model.WhereFragments.Add(
                        PredicateTranslator.Translate(GetLambda(call.Arguments[1]), parameters));
                    break;
```

And add this helper method to the `SqlTranslator` class (e.g. after `Apply`):
```csharp
        // Operator lambda arguments are wrapped in a Quote unary node.
        private static LambdaExpression GetLambda(Expression argument)
        {
            if (argument is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                argument = quote.Operand;
            return (LambdaExpression)argument;
        }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~Where_"`
Expected: PASS — all six `Where_*` tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Sandbox.Core/Sql/PredicateTranslator.cs Sandbox.Core/Sql/SqlTranslator.cs Sandbox.Tests/SqlQueryBuilderTests.cs
git commit -m "feat(sql): translate WHERE predicates (comparisons, AND/OR/NOT, LIKE, NULL, funcletization)"
```

---

## Task 3: `ORDER BY` (OrderBy / OrderByDescending / ThenBy / ThenByDescending)

**Files:**
- Modify: `Sandbox.Core/Sql/SqlTranslator.cs` (add four cases)
- Modify: `Sandbox.Tests/SqlQueryBuilderTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `SqlQueryBuilderTests`:
```csharp
        [TestMethod]
        public void OrderBy_ThenByDescending_EmitsOrderByClause()
        {
            var compiled = SqlQuery.From("Products")
                .OrderBy(r => r["Category"])
                .ThenByDescending(r => r["Price"])
                .ToSql();

            Assert.AreEqual("SELECT * FROM [Products] ORDER BY [Category], [Price] DESC", compiled.Sql);
            Assert.AreEqual(0, compiled.Parameters.Count);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~OrderBy_ThenByDescending"`
Expected: FAIL — `OrderBy` is an unsupported operator (NotSupportedException).

- [ ] **Step 3: Add the ordering cases to `Apply`**

In `Sandbox.Core/Sql/SqlTranslator.cs`, add these cases to the `Apply` switch (above `default`):
```csharp
                case "OrderBy":
                case "ThenBy":
                    model.OrderBy.Add((PredicateTranslator.Column(GetLambda(call.Arguments[1])), false));
                    break;

                case "OrderByDescending":
                case "ThenByDescending":
                    model.OrderBy.Add((PredicateTranslator.Column(GetLambda(call.Arguments[1])), true));
                    break;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~OrderBy_ThenByDescending"`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Sandbox.Core/Sql/SqlTranslator.cs Sandbox.Tests/SqlQueryBuilderTests.cs
git commit -m "feat(sql): translate OrderBy/ThenBy to ORDER BY"
```

---

## Task 4: Paging (`Skip` / `Take` → `OFFSET … FETCH NEXT …`)

**Files:**
- Modify: `Sandbox.Core/Sql/SqlTranslator.cs` (add `Skip`/`Take` cases + `GetInt` helper)
- Modify: `Sandbox.Tests/SqlQueryBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `SqlQueryBuilderTests`:
```csharp
        [TestMethod]
        public void Paging_WithOrderBy_EmitsOffsetFetch()
        {
            var compiled = SqlQuery.From("Products")
                .OrderBy(r => r["Price"])
                .Skip(10)
                .Take(5)
                .ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] ORDER BY [Price] OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY",
                compiled.Sql);
            Assert.AreEqual(10, compiled.Parameters["@p0"]);
            Assert.AreEqual(5, compiled.Parameters["@p1"]);
        }

        [TestMethod]
        public void Paging_WithoutOrderBy_EmitsOrderBySelect1()
        {
            var compiled = SqlQuery.From("Products").Skip(10).Take(5).ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] ORDER BY (SELECT 1) OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY",
                compiled.Sql);
            Assert.AreEqual(10, compiled.Parameters["@p0"]);
            Assert.AreEqual(5, compiled.Parameters["@p1"]);
        }

        [TestMethod]
        public void Take_Only_DefaultsOffsetToZero()
        {
            var compiled = SqlQuery.From("Products").Take(5).ToSql();

            Assert.AreEqual(
                "SELECT * FROM [Products] ORDER BY (SELECT 1) OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY",
                compiled.Sql);
            Assert.AreEqual(0, compiled.Parameters["@p0"]);
            Assert.AreEqual(5, compiled.Parameters["@p1"]);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~Paging_|FullyQualifiedName~Take_Only"`
Expected: FAIL — `Skip`/`Take` are unsupported operators.

- [ ] **Step 3: Add the paging cases + helper to `Apply`**

In `Sandbox.Core/Sql/SqlTranslator.cs`, add these cases to the `Apply` switch (above `default`):
```csharp
                case "Skip":
                    model.Skip = GetInt(call.Arguments[1]);
                    break;

                case "Take":
                    model.Take = GetInt(call.Arguments[1]);
                    break;
```

And add this helper to the `SqlTranslator` class (next to `GetLambda`):
```csharp
        // Skip/Take argument may be a literal or a captured variable; evaluate it.
        private static int GetInt(Expression argument)
            => (int)Expression.Lambda(argument).Compile().DynamicInvoke()!;
```

(The `Render` method already emits OFFSET/FETCH and the `ORDER BY (SELECT 1)` fallback — no render change needed.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~Paging_|FullyQualifiedName~Take_Only"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```powershell
git add Sandbox.Core/Sql/SqlTranslator.cs Sandbox.Tests/SqlQueryBuilderTests.cs
git commit -m "feat(sql): translate Skip/Take to OFFSET/FETCH paging"
```

---

## Task 5: Projection (`Select`) + composed query + unsupported cases

**Files:**
- Modify: `Sandbox.Core/Sql/SqlTranslator.cs` (add `Select` case)
- Modify: `Sandbox.Tests/SqlQueryBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `SqlQueryBuilderTests`:
```csharp
        [TestMethod]
        public void Select_AnonymousType_EmitsColumnList()
        {
            var compiled = SqlQuery.From("Products")
                .Select(r => new { Id = r["Id"], Name = r["Name"] })
                .ToSql();

            Assert.AreEqual("SELECT [Id], [Name] FROM [Products]", compiled.Sql);
        }

        [TestMethod]
        public void Composed_Where_Order_Page_Project_NumbersParamsInRenderOrder()
        {
            var compiled = SqlQuery.From("Products")
                .Where(r => (decimal)r["Price"] > 50m)
                .OrderByDescending(r => r["Price"])
                .Skip(10)
                .Take(5)
                .Select(r => new { Name = r["Name"] })
                .ToSql();

            Assert.AreEqual(
                "SELECT [Name] FROM [Products] WHERE [Price] > @p0 ORDER BY [Price] DESC " +
                "OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY",
                compiled.Sql);
            Assert.AreEqual(50m, compiled.Parameters["@p0"]);
            Assert.AreEqual(10, compiled.Parameters["@p1"]);
            Assert.AreEqual(5, compiled.Parameters["@p2"]);
        }

        [TestMethod]
        public void ComputedProjection_Throws()
        {
            Assert.ThrowsExactly<NotSupportedException>(() =>
                SqlQuery.From("Products")
                    .Select(r => new { Total = (decimal)r["Price"] + 1m })
                    .ToSql());
        }

        [TestMethod]
        public void UnsupportedOperator_Throws()
        {
            Assert.ThrowsExactly<NotSupportedException>(() =>
                SqlQuery.From("Products").Distinct().ToSql());
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj --filter "FullyQualifiedName~Select_|FullyQualifiedName~Composed_|FullyQualifiedName~ComputedProjection|FullyQualifiedName~UnsupportedOperator"`
Expected: FAIL — `Select_*` and `Composed_*` fail (Select unsupported); the two `*Throws` tests already pass (Select/Distinct hit the `default` NotSupportedException), which is acceptable — they must still pass after Step 3.

- [ ] **Step 3: Add the `Select` case to `Apply`**

In `Sandbox.Core/Sql/SqlTranslator.cs`, add this case to the `Apply` switch (above `default`):
```csharp
                case "Select":
                    model.SelectColumns.AddRange(
                        PredicateTranslator.ProjectionColumns(GetLambda(call.Arguments[1])));
                    break;
```

(`ComputedProjection_Throws` now exercises the real projection path — `ProjectionColumns` throws on the `+` expression. `UnsupportedOperator_Throws` still hits `default` for `Distinct`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Sandbox.Tests/Sandbox.Tests.csproj`
Expected: PASS — the full suite is green (all prior tests + the new ones).

- [ ] **Step 5: Commit**

```powershell
git add Sandbox.Core/Sql/SqlTranslator.cs Sandbox.Tests/SqlQueryBuilderTests.cs
git commit -m "feat(sql): translate Select projection; full query composition"
```

---

## Self-Review Notes (plan vs spec)

- **Provider plumbing + bare SELECT** → Task 1 (`SqlQueryable<T> : IOrderedQueryable<T>`, `SqlQuery.From`, `SqlQueryProvider`, `CompiledSql`, `ToSql`, `SqlQueryModel`, `SqlTranslator` chain-walk + render + `SqlParameterBag`).
- **Column refs via `row["Col"]` + cast unwrap** → Task 2 `AsColumn`/`Unwrap`.
- **Comparisons (both operand orders), logical AND/OR/NOT** → Task 2 `VisitComparison`/`VisitBool` + `Flip`.
- **Funcletization of non-row subtrees** → Task 2 `Evaluate` + `RowReferenceFinder`.
- **LIKE (Contains/StartsWith/EndsWith), NULL (IS NULL/IS NOT NULL)** → Task 2 `VisitLike`/`IsNull`.
- **ORDER BY (OrderBy/Descending/ThenBy/ThenByDescending)** → Task 3.
- **Paging OFFSET/FETCH, `ORDER BY (SELECT 1)` fallback, Take-only OFFSET 0** → Task 4 + `Render` (written in Task 1).
- **Projection (anonymous/array), SELECT \*** → Task 5 `ProjectionColumns`/`CollectColumns` + `Render`.
- **Deterministic param numbering (WHERE → Skip → Take)** → `SqlParameterBag` + render order; asserted by `Composed_*`.
- **Unsupported nodes throw `NotSupportedException`** → `default` case, `ProjectionColumns`, predicate `default`; asserted by `ComputedProjection_Throws`/`UnsupportedOperator_Throws`.
- **Note:** `Render` lives in `SqlTranslator` (with the parameter bag) rather than on `SqlQueryModel`; the model is a plain data holder. This is a deliberate, minor deviation from the spec's component table for cohesion.
- Type/name consistency: `SqlQuery.From`, `SqlQueryable<T>`, `SqlQueryProvider.Table`, `CompiledSql.Sql/Parameters`, `ToSql`, `SqlQueryModel.{Table,SelectColumns,WhereFragments,OrderBy,Skip,Take}`, `SqlParameterBag.Add`, `PredicateTranslator.{Translate,Column,ProjectionColumns}`, `GetLambda`/`GetInt` — used identically across tasks.
