using sandbox_api.Data;
using sandbox_api.Models;
using sandbox_api.Utils;

namespace sandbox_api.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly SimulatedDatabase _db;
        private readonly IProductRepository _productRepository;
        private readonly IUserRepository _userRepository;

        public OrderRepository(SimulatedDatabase db, IProductRepository productRepository, IUserRepository userRepository)
        {
            _db = db;
            _productRepository = productRepository;
            _userRepository = userRepository;
        }

        public async Task<Result<List<Order>, DatabaseError>> GetAllOrdersAsync()
        {
            return await ResultHelpers.TryAsync(
                async () => await _db.GetAllOrdersAsync(),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve orders", ex)
            );
        }

        public async Task<Result<Order, DatabaseError>> GetOrderByIdAsync(int id)
        {
            var result = await ResultHelpers.TryAsync(
                async () => await _db.GetOrderByIdAsync(id),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve order", ex)
            );

            return result.Bind(order =>
                order != null
                    ? Result<Order, DatabaseError>.Success(order)
                    : new NotFoundError("Order", id.ToString())
            );
        }

        public async Task<Result<List<Order>, DatabaseError>> GetOrdersByUserIdAsync(int userId)
        {
            // Verify user exists
            var userResult = await _userRepository.GetUserByIdAsync(userId);
            if (userResult.IsFailure)
                return userResult.Error;

            return await ResultHelpers.TryAsync(
                async () => await _db.GetOrdersByUserIdAsync(userId),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve user orders", ex)
            );
        }

        public async Task<Result<Order, DatabaseError>> CreateOrderAsync(int userId, List<(int ProductId, int Quantity)> items)
        {
            // Validation
            if (items == null || items.Count == 0)
                return new ValidationError("Items", "Order must contain at least one item");

            // Verify user exists
            var userResult = await _userRepository.GetUserByIdAsync(userId);
            if (userResult.IsFailure)
                return userResult.Error;

            var user = userResult.Value;

            if (!user.IsActive)
                return new InvalidOperationError("CreateOrder", "Cannot create order for inactive user");

            // Validate all products and check stock using Traverse
            var orderItemsResult = await ResultHelpers.TraverseAsync(
                items,
                async item =>
                {
                    if (item.Quantity <= 0)
                        return Result<OrderItem, DatabaseError>.Failure(
                            new ValidationError("Quantity", "Quantity must be greater than zero"));

                    var productResult = await _productRepository.GetProductByIdAsync(item.ProductId);
                    if (productResult.IsFailure)
                        return productResult.Error;

                    var product = productResult.Value;

                    if (!product.IsAvailable)
                        return new InvalidOperationError("CreateOrder", $"Product '{product.Name}' is not available");

                    if (product.StockQuantity < item.Quantity)
                        return new InsufficientStockError(product.Name, item.Quantity, product.StockQuantity);

                    return Result<OrderItem, DatabaseError>.Success(new OrderItem
                    {
                        ProductId = product.Id,
                        Product = product,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    });
                }
            );

            if (orderItemsResult.IsFailure)
                return orderItemsResult.Error;

            var orderItems = orderItemsResult.Value;

            // Reserve stock for all items using Traverse
            var reserveResult = await ResultHelpers.TraverseAsync(
                items,
                async item => await _productRepository.ReserveStockAsync(item.ProductId, item.Quantity)
            );

            if (reserveResult.IsFailure)
                return reserveResult.Error;

            // Create the order
            var order = new Order
            {
                UserId = userId,
                User = user,
                Items = orderItems,
                TotalAmount = orderItems.Sum(i => i.Subtotal),
                Status = OrderStatus.Pending
            };

            return await ResultHelpers.TryAsync(
                async () => await _db.AddOrderAsync(order),
                ex => new DatabaseError("DB_ERROR", "Failed to create order", ex)
            );
        }

        public async Task<Result<Order, DatabaseError>> UpdateOrderStatusAsync(int orderId, OrderStatus status)
        {
            var orderResult = await GetOrderByIdAsync(orderId);
            if (orderResult.IsFailure)
                return orderResult.Error;

            var order = orderResult.Value;

            // Validate status transition
            if (order.Status == OrderStatus.Delivered || order.Status == OrderStatus.Cancelled)
                return new InvalidOperationError("UpdateOrderStatus", $"Cannot update status of {order.Status} order");

            if (status == OrderStatus.Pending && order.Status != OrderStatus.Pending)
                return new InvalidOperationError("UpdateOrderStatus", "Cannot revert order to Pending status");

            var updateResult = await ResultHelpers.TryAsync(
                async () => await _db.UpdateOrderStatusAsync(orderId, status),
                ex => new DatabaseError("DB_ERROR", "Failed to update order status", ex)
            );

            return updateResult.Bind(success =>
            {
                if (!success)
                    return new DatabaseError("UPDATE_FAILED", "Failed to update order status");

                order.Status = status;
                if (status == OrderStatus.Delivered || status == OrderStatus.Cancelled)
                {
                    order.CompletedAt = DateTime.UtcNow;
                }
                return Result<Order, DatabaseError>.Success(order);
            });
        }

        public async Task<Result<Order, DatabaseError>> CancelOrderAsync(int orderId)
        {
            var orderResult = await GetOrderByIdAsync(orderId);
            if (orderResult.IsFailure)
                return orderResult.Error;

            var order = orderResult.Value;

            if (order.Status == OrderStatus.Delivered)
                return new InvalidOperationError("CancelOrder", "Cannot cancel delivered order");

            if (order.Status == OrderStatus.Cancelled)
                return new InvalidOperationError("CancelOrder", "Order is already cancelled");

            // Restore stock for all items if order was in processing
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing)
            {
                var restoreResult = await ResultHelpers.TraverseAsync(
                    order.Items,
                    async item => await _productRepository.AdjustStockAsync(item.ProductId, item.Quantity)
                );

                if (restoreResult.IsFailure)
                    return restoreResult.Error;
            }

            return await UpdateOrderStatusAsync(orderId, OrderStatus.Cancelled);
        }
    }
}
