using System.Collections;
using System.Linq.Expressions;

namespace sandbox_api.Models
{
	public enum OrderStatus
	{
		Pending,
		Processing,
		Shipped,
		Delivered,
		Cancelled
	}

	public class QueryableOrder : IQueryable<Order>
	{
		private readonly IQueryProvider _provider;
		private readonly Expression _expression;

		public QueryableOrder(IEnumerable<Order> source)
			: this(new OrderQueryProvider(source), null)
		{
		}

		internal QueryableOrder(IQueryProvider provider, Expression? expression)
		{
			_provider = provider;
			_expression = expression ?? Expression.Constant(this);
		}

		public Type ElementType => typeof(Order);
		public Expression Expression => _expression;
		public IQueryProvider Provider => _provider;

		public IEnumerator<Order> GetEnumerator()
			=> Provider.Execute<IEnumerable<Order>>(Expression).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	internal sealed class OrderQueryProvider : IQueryProvider
	{
		private readonly IEnumerable<Order> _source;

		public OrderQueryProvider(IEnumerable<Order> source) => _source = source;

		public IQueryable CreateQuery(Expression expression)
			=> new QueryableOrder(this, expression);

		public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
		{
			if (typeof(TElement) != typeof(Order))
				throw new NotSupportedException($"Only {nameof(Order)} queries are supported.");

			return (IQueryable<TElement>)(object)new QueryableOrder(this, expression);
		}

		public object? Execute(Expression expression)
			=> Execute<IEnumerable<Order>>(expression);

		public TResult Execute<TResult>(Expression expression)
		{
			var rewritten = new OrderExpressionRewriter(_source).Visit(expression);
			return Expression.Lambda<Func<TResult>>(rewritten!).Compile()();
		}
	}

	internal sealed class OrderExpressionRewriter : ExpressionVisitor
	{
		private readonly IEnumerable<Order> _source;

		public OrderExpressionRewriter(IEnumerable<Order> source) => _source = source;

		protected override Expression VisitConstant(ConstantExpression node)
		{
			if (node.Value is IQueryable<Order>)
				return Expression.Constant(_source);

			return node;
		}
	}

	public class Order
	{
		public int Id { get; set; }
		public int UserId { get; set; }
		public User? User { get; set; }
		public List<OrderItem> Items { get; set; } = new();
		public decimal TotalAmount { get; set; }
		public OrderStatus Status { get; set; } = OrderStatus.Pending;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? CompletedAt { get; set; }
	}

	public class OrderItem
	{
		public int Id { get; set; }
		public int OrderId { get; set; }
		public int ProductId { get; set; }
		public Product? Product { get; set; }
		public int Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal Subtotal => Quantity * UnitPrice;
	}
}
