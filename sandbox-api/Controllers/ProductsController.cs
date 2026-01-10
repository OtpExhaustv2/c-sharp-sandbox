using Microsoft.AspNetCore.Mvc;
using sandbox_api.Models;
using sandbox_api.Repositories;

namespace sandbox_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _productRepository;

        public ProductsController(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        /// <summary>
        /// Get all products
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            var result = await _productRepository.GetAllProductsAsync();

            return result.Match<IActionResult>(
                products => Ok(products),
                error => StatusCode(500, new { error.Code, error.Message })
            );
        }

        /// <summary>
        /// Get only available products
        /// </summary>
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableProducts()
        {
            var result = await _productRepository.GetAvailableProductsAsync();

            return result.Match<IActionResult>(
                products => Ok(products),
                error => StatusCode(500, new { error.Code, error.Message })
            );
        }

        /// <summary>
        /// Get product by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var result = await _productRepository.GetProductByIdAsync(id);

            return result.Match<IActionResult>(
                product => Ok(product),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Get products by category
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetProductsByCategory(string category)
        {
            var result = await _productRepository.GetProductsByCategoryAsync(category);

            return result.Match<IActionResult>(
                products => Ok(products),
                error => error switch
                {
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Create a new product
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            var result = await _productRepository.CreateProductAsync(
                request.Name,
                request.Description,
                request.Price,
                request.StockQuantity,
                request.Category
            );

            return result.Match<IActionResult>(
                product => CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product),
                error => error switch
                {
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Update an existing product
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
        {
            var result = await _productRepository.UpdateProductAsync(
                id,
                request.Name,
                request.Description,
                request.Price,
                request.StockQuantity,
                request.Category
            );

            return result.Match<IActionResult>(
                product => Ok(product),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Adjust product stock (add or remove)
        /// </summary>
        [HttpPatch("{id}/stock/adjust")]
        public async Task<IActionResult> AdjustStock(int id, [FromBody] AdjustStockRequest request)
        {
            var result = await _productRepository.AdjustStockAsync(id, request.QuantityChange);

            return result.Match<IActionResult>(
                _ => Ok(new { message = "Stock adjusted successfully" }),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }

        /// <summary>
        /// Reserve stock for a product (used internally by orders)
        /// </summary>
        [HttpPost("{id}/stock/reserve")]
        public async Task<IActionResult> ReserveStock(int id, [FromBody] ReserveStockRequest request)
        {
            var result = await _productRepository.ReserveStockAsync(id, request.Quantity);

            return result.Match<IActionResult>(
                _ => Ok(new { message = "Stock reserved successfully" }),
                error => error switch
                {
                    NotFoundError => NotFound(new { error.Code, error.Message }),
                    ValidationError => BadRequest(new { error.Code, error.Message }),
                    InsufficientStockError => BadRequest(new { error.Code, error.Message }),
                    InvalidOperationError => BadRequest(new { error.Code, error.Message }),
                    _ => StatusCode(500, new { error.Code, error.Message })
                }
            );
        }
    }

    // DTOs
    public record CreateProductRequest(string Name, string Description, decimal Price, int StockQuantity, string Category);
    public record UpdateProductRequest(string? Name, string? Description, decimal? Price, int? StockQuantity, string? Category);
    public record AdjustStockRequest(int QuantityChange);
    public record ReserveStockRequest(int Quantity);
}
