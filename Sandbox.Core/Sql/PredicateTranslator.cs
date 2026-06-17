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
                var argument = EscapeLike(Evaluate(call.Arguments[0])?.ToString());
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
            if (node is ConstantExpression constant)
                return constant.Value;
            return Expression.Lambda(node).Compile().DynamicInvoke();
        }

        // Escape T-SQL LIKE metacharacters so a search term is matched literally.
        // '[' first, so the brackets we introduce for '%' and '_' are not re-escaped.
        private static string? EscapeLike(string? value)
            => value?.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

        private bool ReferencesRow(Expression node) => new RowReferenceFinder(_row).IsFound(node);

        private static Expression Unwrap(Expression node)
            => node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u
                ? Unwrap(u.Operand)
                : node;

        private string RequireColumn(Expression node)
            => AsColumn(node) ?? throw new NotSupportedException("Expected a row[\"Col\"] reference.");

        private static bool IsNull(Expression node)
            => Unwrap(node) is ConstantExpression { Value: null };

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
