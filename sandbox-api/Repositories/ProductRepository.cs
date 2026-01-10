using sandbox_api.Data;
using sandbox_api.Models;
using sandbox_api.Utils;

namespace sandbox_api.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly SimulatedDatabase _db;

        public ProductRepository(SimulatedDatabase db)
        {
            _db = db;
        }

        public async Task<Result<List<Product>, DatabaseError>> GetAllProductsAsync()
        {
            return await ResultHelpers.TryAsync(
                async () => await _db.GetAllProductsAsync(),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve products", ex)
            );
        }

        public async Task<Result<List<Product>, DatabaseError>> GetAvailableProductsAsync()
        {
            var result = await GetAllProductsAsync();
            return result.Map(products => products.Where(p => p.IsAvailable && p.StockQuantity > 0).ToList());
        }

        public async Task<Result<Product, DatabaseError>> GetProductByIdAsync(int id)
        {
            var result = await ResultHelpers.TryAsync(
                async () => await _db.GetProductByIdAsync(id),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve product", ex)
            );

            return result.Bind(product =>
                product != null
                    ? Result<Product, DatabaseError>.Success(product)
                    : new NotFoundError("Product", id.ToString())
            );
        }

        public async Task<Result<List<Product>, DatabaseError>> GetProductsByCategoryAsync(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return new ValidationError("Category", "Category cannot be empty");
            }

            return await ResultHelpers.TryAsync(
                async () => await _db.GetProductsByCategoryAsync(category),
                ex => new DatabaseError("DB_ERROR", "Failed to retrieve products by category", ex)
            );
        }

        public async Task<Result<Product, DatabaseError>> CreateProductAsync(
            string name,
            string description,
            decimal price,
            int stockQuantity,
            string category)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(name))
                return new ValidationError("Name", "Product name cannot be empty");

            if (string.IsNullOrWhiteSpace(description))
                return new ValidationError("Description", "Product description cannot be empty");

            if (price <= 0)
                return new ValidationError("Price", "Price must be greater than zero");

            if (stockQuantity < 0)
                return new ValidationError("StockQuantity", "Stock quantity cannot be negative");

            if (string.IsNullOrWhiteSpace(category))
                return new ValidationError("Category", "Category cannot be empty");

            var newProduct = new Product
            {
                Name = name,
                Description = description,
                Price = price,
                StockQuantity = stockQuantity,
                Category = category,
                IsAvailable = stockQuantity > 0
            };

            return await ResultHelpers.TryAsync(
                async () => await _db.AddProductAsync(newProduct),
                ex => new DatabaseError("DB_ERROR", "Failed to create product", ex)
            );
        }

        public async Task<Result<Product, DatabaseError>> UpdateProductAsync(
            int id,
            string? name,
            string? description,
            decimal? price,
            int? stockQuantity,
            string? category)
        {
            // Get existing product
            var getProductResult = await GetProductByIdAsync(id);
            if (getProductResult.IsFailure)
                return getProductResult.Error;

            var product = getProductResult.Value;

            // Update only provided fields
            if (!string.IsNullOrWhiteSpace(name))
                product.Name = name;

            if (!string.IsNullOrWhiteSpace(description))
                product.Description = description;

            if (price.HasValue)
            {
                if (price.Value <= 0)
                    return new ValidationError("Price", "Price must be greater than zero");
                product.Price = price.Value;
            }

            if (stockQuantity.HasValue)
            {
                if (stockQuantity.Value < 0)
                    return new ValidationError("StockQuantity", "Stock quantity cannot be negative");
                product.StockQuantity = stockQuantity.Value;
                product.IsAvailable = stockQuantity.Value > 0;
            }

            if (!string.IsNullOrWhiteSpace(category))
                product.Category = category;

            var updateResult = await ResultHelpers.TryAsync(
                async () => await _db.UpdateProductAsync(product),
                ex => new DatabaseError("DB_ERROR", "Failed to update product", ex)
            );

            return updateResult.Bind(success =>
                success
                    ? Result<Product, DatabaseError>.Success(product)
                    : new DatabaseError("UPDATE_FAILED", "Failed to update product")
            );
        }

        public async Task<Result<Utils.Unit, DatabaseError>> AdjustStockAsync(int productId, int quantityChange)
        {
            var getProductResult = await GetProductByIdAsync(productId);
            if (getProductResult.IsFailure)
                return getProductResult.Error;

            var product = getProductResult.Value;
            var newQuantity = product.StockQuantity + quantityChange;

            if (newQuantity < 0)
                return new ValidationError("StockQuantity", $"Insufficient stock. Current: {product.StockQuantity}, Requested change: {quantityChange}");

            var updateResult = await ResultHelpers.TryAsync(
                async () => await _db.UpdateProductStockAsync(productId, newQuantity),
                ex => new DatabaseError("DB_ERROR", "Failed to adjust stock", ex)
            );

            return updateResult.Bind(success =>
                success
                    ? Result<Utils.Unit, DatabaseError>.Success(Utils.Unit.Value)
                    : new DatabaseError("UPDATE_FAILED", "Failed to adjust stock")
            );
        }

        public async Task<Result<Utils.Unit, DatabaseError>> ReserveStockAsync(int productId, int quantity)
        {
            if (quantity <= 0)
                return new ValidationError("Quantity", "Quantity must be greater than zero");

            var getProductResult = await GetProductByIdAsync(productId);
            if (getProductResult.IsFailure)
                return getProductResult.Error;

            var product = getProductResult.Value;

            if (!product.IsAvailable)
                return new InvalidOperationError("ReserveStock", $"Product '{product.Name}' is not available");

            if (product.StockQuantity < quantity)
                return new InsufficientStockError(product.Name, quantity, product.StockQuantity);

            return await AdjustStockAsync(productId, -quantity);
        }
    }
}
