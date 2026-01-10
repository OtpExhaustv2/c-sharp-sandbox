using sandbox_api.Models;

namespace sandbox_api.Data
{
    /// <summary>
    /// Simulated in-memory database with realistic delay to mimic actual database operations
    /// </summary>
    public class SimulatedDatabase
    {
        private readonly List<User> _users = new();
        private readonly List<Product> _products = new();
        private readonly List<Order> _orders = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        private int _nextUserId = 1;
        private int _nextProductId = 1;
        private int _nextOrderId = 1;

        public SimulatedDatabase()
        {
            SeedData();
        }

        // Simulated network/database delay
        private async Task SimulateDelay()
        {
            await Task.Delay(Random.Shared.Next(50, 150));
        }

        // Seed initial data
        private void SeedData()
        {
            // Seed users
            _users.AddRange(new[]
            {
                new User { Id = _nextUserId++, Username = "john_doe", Email = "john@example.com", FullName = "John Doe", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new User { Id = _nextUserId++, Username = "jane_smith", Email = "jane@example.com", FullName = "Jane Smith", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-25) },
                new User { Id = _nextUserId++, Username = "bob_wilson", Email = "bob@example.com", FullName = "Bob Wilson", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new User { Id = _nextUserId++, Username = "alice_brown", Email = "alice@example.com", FullName = "Alice Brown", IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-60) }
            });

            // Seed products
            _products.AddRange(new[]
            {
                new Product { Id = _nextProductId++, Name = "Laptop", Description = "High-performance laptop", Price = 999.99m, StockQuantity = 10, Category = "Electronics", IsAvailable = true },
                new Product { Id = _nextProductId++, Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, StockQuantity = 50, Category = "Electronics", IsAvailable = true },
                new Product { Id = _nextProductId++, Name = "Keyboard", Description = "Mechanical keyboard", Price = 79.99m, StockQuantity = 30, Category = "Electronics", IsAvailable = true },
                new Product { Id = _nextProductId++, Name = "Monitor", Description = "27-inch 4K monitor", Price = 399.99m, StockQuantity = 5, Category = "Electronics", IsAvailable = true },
                new Product { Id = _nextProductId++, Name = "Headphones", Description = "Noise-cancelling headphones", Price = 199.99m, StockQuantity = 0, Category = "Electronics", IsAvailable = false }
            });

            // Seed some orders
            var order1 = new Order
            {
                Id = _nextOrderId++,
                UserId = 1,
                Status = OrderStatus.Delivered,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                CompletedAt = DateTime.UtcNow.AddDays(-8),
                Items = new List<OrderItem>
                {
                    new OrderItem { Id = 1, ProductId = 1, Quantity = 1, UnitPrice = 999.99m },
                    new OrderItem { Id = 2, ProductId = 2, Quantity = 2, UnitPrice = 29.99m }
                }
            };
            order1.TotalAmount = order1.Items.Sum(i => i.Subtotal);
            _orders.Add(order1);
        }

        // User operations
        public async Task<List<User>> GetAllUsersAsync()
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _users.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _users.FirstOrDefault(u => u.Id == id);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<User> AddUserAsync(User user)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                user.Id = _nextUserId++;
                user.CreatedAt = DateTime.UtcNow;
                _users.Add(user);
                return user;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                var existingUser = _users.FirstOrDefault(u => u.Id == user.Id);
                if (existingUser == null) return false;

                existingUser.Username = user.Username;
                existingUser.Email = user.Email;
                existingUser.FullName = user.FullName;
                existingUser.IsActive = user.IsActive;
                existingUser.LastLoginAt = user.LastLoginAt;
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                var user = _users.FirstOrDefault(u => u.Id == id);
                if (user == null) return false;

                _users.Remove(user);
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        // Product operations
        public async Task<List<Product>> GetAllProductsAsync()
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _products.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _products.FirstOrDefault(p => p.Id == id);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<Product>> GetProductsByCategoryAsync(string category)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Product> AddProductAsync(Product product)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                product.Id = _nextProductId++;
                product.CreatedAt = DateTime.UtcNow;
                _products.Add(product);
                return product;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> UpdateProductAsync(Product product)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                var existingProduct = _products.FirstOrDefault(p => p.Id == product.Id);
                if (existingProduct == null) return false;

                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.Price = product.Price;
                existingProduct.StockQuantity = product.StockQuantity;
                existingProduct.Category = product.Category;
                existingProduct.IsAvailable = product.IsAvailable;
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> UpdateProductStockAsync(int productId, int quantity)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                var product = _products.FirstOrDefault(p => p.Id == productId);
                if (product == null) return false;

                product.StockQuantity = quantity;
                product.IsAvailable = quantity > 0;
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        // Order operations
        public async Task<List<Order>> GetAllOrdersAsync()
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _orders.ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _orders.FirstOrDefault(o => o.Id == id);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<Order>> GetOrdersByUserIdAsync(int userId)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                return _orders.Where(o => o.UserId == userId).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Order> AddOrderAsync(Order order)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                order.Id = _nextOrderId++;
                order.CreatedAt = DateTime.UtcNow;
                _orders.Add(order);
                return order;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus status)
        {
            await SimulateDelay();
            await _lock.WaitAsync();
            try
            {
                var order = _orders.FirstOrDefault(o => o.Id == orderId);
                if (order == null) return false;

                order.Status = status;
                if (status == OrderStatus.Delivered || status == OrderStatus.Cancelled)
                {
                    order.CompletedAt = DateTime.UtcNow;
                }
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
