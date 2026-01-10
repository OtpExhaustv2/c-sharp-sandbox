using sandbox_api.Models;
using sandbox_api.Utils;

namespace sandbox_api.Repositories
{
    public interface IProductRepository
    {
        Task<Result<List<Product>, DatabaseError>> GetAllProductsAsync();
        Task<Result<List<Product>, DatabaseError>> GetAvailableProductsAsync();
        Task<Result<Product, DatabaseError>> GetProductByIdAsync(int id);
        Task<Result<List<Product>, DatabaseError>> GetProductsByCategoryAsync(string category);
        Task<Result<Product, DatabaseError>> CreateProductAsync(string name, string description, decimal price, int stockQuantity, string category);
        Task<Result<Product, DatabaseError>> UpdateProductAsync(int id, string? name, string? description, decimal? price, int? stockQuantity, string? category);
        Task<Result<Utils.Unit, DatabaseError>> AdjustStockAsync(int productId, int quantityChange);
        Task<Result<Utils.Unit, DatabaseError>> ReserveStockAsync(int productId, int quantity);
    }
}
