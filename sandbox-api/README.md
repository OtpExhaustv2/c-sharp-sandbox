# Sandbox API - Result Pattern Demonstration

This Web API demonstrates the use of the **Result pattern** for database operations, providing type-safe error handling without exceptions.

## Features

- **Result Pattern**: All database operations return `Result<T, TError>` instead of throwing exceptions
- **Simulated Database**: In-memory database with realistic delays to mimic actual database calls
- **Comprehensive Error Types**: Specific error types for different failure scenarios
- **Repository Pattern**: Clean separation of concerns with repository interfaces
- **Functional Composition**: Uses `Map`, `Bind`, `Traverse`, and other functional operations

## Architecture

### Models
- **User**: User accounts with authentication tracking
- **Product**: Product catalog with stock management
- **Order**: Orders with items and status tracking

### Error Types
- `NotFoundError`: Entity not found in database
- `ValidationError`: Invalid input data
- `DuplicateError`: Duplicate unique fields
- `InsufficientStockError`: Not enough stock for order
- `InvalidOperationError`: Business rule violation
- `ConcurrencyError`: Concurrent modification detected
- `ConnectionError`: Database connection issues
- `QueryError`: Query execution failures

### Repositories
All repositories use the Result pattern:
- `IUserRepository`: User CRUD operations
- `IProductRepository`: Product and inventory management
- `IOrderRepository`: Order processing with stock management

## API Endpoints

### Users API (`/api/users`)
```
GET    /api/users              - Get all users
GET    /api/users/{id}         - Get user by ID
GET    /api/users/by-email/{email} - Get user by email
POST   /api/users              - Create new user
PUT    /api/users/{id}         - Update user
DELETE /api/users/{id}         - Delete user
POST   /api/users/{id}/login   - Record user login
```

### Products API (`/api/products`)
```
GET    /api/products                    - Get all products
GET    /api/products/available          - Get available products
GET    /api/products/{id}               - Get product by ID
GET    /api/products/category/{category} - Get products by category
POST   /api/products                    - Create new product
PUT    /api/products/{id}               - Update product
PATCH  /api/products/{id}/stock/adjust  - Adjust stock
POST   /api/products/{id}/stock/reserve - Reserve stock
```

### Orders API (`/api/orders`)
```
GET    /api/orders           - Get all orders
GET    /api/orders/{id}      - Get order by ID
GET    /api/orders/user/{userId} - Get orders by user
POST   /api/orders           - Create new order
PATCH  /api/orders/{id}/status - Update order status
POST   /api/orders/{id}/cancel - Cancel order
```

## Result Pattern Examples

### Simple Query with Error Handling
```csharp
public async Task<Result<User, DatabaseError>> GetUserByIdAsync(int id)
{
    var result = await ResultHelpers.TryAsync(
        async () => await _db.GetUserByIdAsync(id),
        ex => new DatabaseError("DB_ERROR", "Failed to retrieve user", ex)
    );

    return result.Bind(user =>
        user != null
            ? Result<User, DatabaseError>.Success(user)
            : new NotFoundError("User", id.ToString())
    );
}
```

### Complex Operation with Validation
```csharp
public async Task<Result<User, DatabaseError>> CreateUserAsync(string username, string email, string fullName)
{
    // Validation
    if (string.IsNullOrWhiteSpace(email))
        return new ValidationError("Email", "Email cannot be empty");

    // Check for duplicates
    var existingByEmail = await _db.GetUserByEmailAsync(email);
    if (existingByEmail != null)
        return new DuplicateError("Email", email);

    // Create user
    var newUser = new User { Username = username, Email = email, FullName = fullName };

    return await ResultHelpers.TryAsync(
        async () => await _db.AddUserAsync(newUser),
        ex => new DatabaseError("DB_ERROR", "Failed to create user", ex)
    );
}
```

### Pipeline with Multiple Operations (Orders)
```csharp
public async Task<Result<Order, DatabaseError>> CreateOrderAsync(int userId, List<(int ProductId, int Quantity)> items)
{
    // Verify user exists
    var userResult = await _userRepository.GetUserByIdAsync(userId);
    if (userResult.IsFailure)
        return userResult.Error;

    // Validate all products using Traverse (fails fast on first error)
    var orderItemsResult = await ResultHelpers.TraverseAsync(
        items,
        async item =>
        {
            var productResult = await _productRepository.GetProductByIdAsync(item.ProductId);
            if (productResult.IsFailure)
                return productResult.Error;

            var product = productResult.Value;

            if (product.StockQuantity < item.Quantity)
                return new InsufficientStockError(product.Name, item.Quantity, product.StockQuantity);

            return Result<OrderItem, DatabaseError>.Success(new OrderItem { ... });
        }
    );

    if (orderItemsResult.IsFailure)
        return orderItemsResult.Error;

    // Reserve stock for all items
    var reserveResult = await ResultHelpers.TraverseAsync(
        items,
        async item => await _productRepository.ReserveStockAsync(item.ProductId, item.Quantity)
    );

    if (reserveResult.IsFailure)
        return reserveResult.Error;

    // Create order
    return await ResultHelpers.TryAsync(
        async () => await _db.AddOrderAsync(order),
        ex => new DatabaseError("DB_ERROR", "Failed to create order", ex)
    );
}
```

### Controller with Pattern Matching
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetUser(int id)
{
    var result = await _userRepository.GetUserByIdAsync(id);

    return result.Match<IActionResult>(
        user => Ok(user),
        error => error switch
        {
            NotFoundError => NotFound(new { error.Code, error.Message }),
            _ => StatusCode(500, new { error.Code, error.Message })
        }
    );
}
```

## Sample Data

The simulated database is pre-seeded with:
- **Users**: john_doe, jane_smith, bob_wilson, alice_brown (inactive)
- **Products**: Laptop ($999.99), Mouse ($29.99), Keyboard ($79.99), Monitor ($399.99), Headphones (out of stock)
- **Orders**: One completed order for john_doe

## Running the API

```bash
cd sandbox-api
dotnet run
```

The API will be available at `https://localhost:5001` (or the port shown in console).

## Testing Examples

### Create a User
```bash
curl -X POST https://localhost:5001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "username": "test_user",
    "email": "test@example.com",
    "fullName": "Test User"
  }'
```

### Create an Order
```bash
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1,
    "items": [
      {"productId": 2, "quantity": 2},
      {"productId": 3, "quantity": 1}
    ]
  }'
```

### Get User Orders
```bash
curl https://localhost:5001/api/orders/user/1
```

## Key Benefits of Result Pattern

1. **No Exception Throwing**: Errors are values, not exceptions
2. **Type Safety**: Compiler ensures you handle all error cases
3. **Composability**: Chain operations with `Map`, `Bind`, `Traverse`
4. **Explicit Error Handling**: Errors are visible in function signatures
5. **Better Performance**: No exception stack unwinding
6. **Functional Style**: Railway-oriented programming pattern
