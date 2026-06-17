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

        // Operator lambda arguments are wrapped in a Quote unary node.
        private static LambdaExpression GetLambda(Expression argument)
        {
            if (argument is UnaryExpression { NodeType: ExpressionType.Quote } quote)
                argument = quote.Operand;
            return (LambdaExpression)argument;
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
