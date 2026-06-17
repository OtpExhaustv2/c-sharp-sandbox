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

            return (model, parameters);
        }

        private static void Apply(MethodCallExpression call, SqlQueryModel model, SqlParameterBag parameters)
        {
            switch (call.Method.Name)
            {
                case "Where":
                    model.WhereFragments.Add(
                        PredicateTranslator.Translate(GetLambda(call.Arguments[1]), parameters));
                    break;

                case "OrderBy":
                case "ThenBy":
                    model.OrderBy.Add((PredicateTranslator.Column(GetLambda(call.Arguments[1])), false));
                    break;

                case "OrderByDescending":
                case "ThenByDescending":
                    model.OrderBy.Add((PredicateTranslator.Column(GetLambda(call.Arguments[1])), true));
                    break;

                case "Skip":
                    model.Skip = GetInt(call.Arguments[1]);
                    break;

                case "Take":
                    model.Take = GetInt(call.Arguments[1]);
                    break;

                case "Select":
                    model.SelectColumns.AddRange(
                        PredicateTranslator.ProjectionColumns(GetLambda(call.Arguments[1])));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported query operator: {call.Method.Name}");
            }
        }

        // Operator lambda arguments are wrapped in a Quote unary node.
        private static LambdaExpression GetLambda(Expression argument)
        {
            if (argument is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                argument = quote.Operand;
            return (LambdaExpression)argument;
        }

        // Skip/Take argument may be a literal or a captured variable; evaluate it.
        private static int GetInt(Expression argument)
            => (int)Expression.Lambda(argument).Compile().DynamicInvoke()!;

        private static string Render(SqlQueryModel m, SqlParameterBag parameters)
        {
            var sb = new StringBuilder();

            sb.Append("SELECT ");
            sb.Append(m.SelectColumns.Count == 0 ? "*" : string.Join(", ", m.SelectColumns));
            sb.Append(" FROM [").Append(m.Table).Append(']');

            AppendWhere(sb, m);

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
