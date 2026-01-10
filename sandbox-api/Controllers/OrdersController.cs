using Microsoft.AspNetCore.Mvc;
using sandbox_api.Models;
using sandbox_api.Repositories;

namespace sandbox_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;

        public OrdersController(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        /// <summary>
        /// Get all orders
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            var result = await _orderRepository.GetAllOrdersAsync();

            return result.Match<IActionResult>(
                orders => Ok(orders),
                error => StatusCode(500, new { error.Code, error.Message })
            );
        }

        /// <summary>
        /// Get order by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            var result = await _orderRepository.GetOrderByIdAsync(id);

            return result.Match<IActionResult>(
                order => Ok(order),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Get orders by user ID
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetOrdersByUser(int userId)
        {
            var result = await _orderRepository.GetOrdersByUserIdAsync(userId);

            return result.Match<IActionResult>(
                orders => Ok(orders),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Create a new order
        /// Demonstrates complex Result pattern usage with multiple validations
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            // Convert DTO items to tuple list
            var items = request.Items
                .Select(item => (item.ProductId, item.Quantity))
                .ToList();

            var result = await _orderRepository.CreateOrderAsync(request.UserId, items);

            return result.Match<IActionResult>(
                order => CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    InvalidOperationError => BadRequest(new { error.Code, error.Message }),
                    InsufficientStockError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Update order status
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var result = await _orderRepository.UpdateOrderStatusAsync(id, request.Status);

            return result.Match<IActionResult>(
                order => Ok(order),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    InvalidOperationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Cancel an order
        /// Demonstrates Result pattern with business logic and side effects (stock restoration)
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var result = await _orderRepository.CancelOrderAsync(id);

            return result.Match<IActionResult>(
                order => Ok(new { message = "Order cancelled successfully", order }),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    InvalidOperationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }
    }

    // DTOs
    public record CreateOrderRequest(int UserId, List<OrderItemRequest> Items);
    public record OrderItemRequest(int ProductId, int Quantity);
    public record UpdateOrderStatusRequest(OrderStatus Status);
}
