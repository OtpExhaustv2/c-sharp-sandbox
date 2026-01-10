using sandbox_api.Models;
using sandbox_api.Utils;

namespace sandbox_api.Repositories
{
    public interface IOrderRepository
    {
        Task<Result<List<Order>, DatabaseError>> GetAllOrdersAsync();
        Task<Result<Order, DatabaseError>> GetOrderByIdAsync(int id);
        Task<Result<List<Order>, DatabaseError>> GetOrdersByUserIdAsync(int userId);
        Task<Result<Order, DatabaseError>> CreateOrderAsync(int userId, List<(int ProductId, int Quantity)> items);
        Task<Result<Order, DatabaseError>> UpdateOrderStatusAsync(int orderId, OrderStatus status);
        Task<Result<Order, DatabaseError>> CancelOrderAsync(int orderId);
    }
}
