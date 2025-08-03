using Microsoft.AspNetCore.Mvc;
using ProductSearchApi.DTOs;
using ProductSearchApi.Services;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly ILogger<ProductController> _logger;
        private readonly IProductService _productService;

        public ProductController(ILogger<ProductController> logger, IProductService productService)
        {
            _logger = logger;
            _productService = productService;
        }

        /// <summary>
        /// Get all products
        /// </summary>
        /// <returns>List of all products</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            _logger.LogInformation("Getting all products");
            
            var products = await _productService.GetAllAsync();
            return Ok(products);
        }

        /// <summary>
        /// Get a specific product by ID
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>Single product or 404 if not found</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            _logger.LogInformation("Getting product with ID: {ProductId}", id);
            
            var product = await _productService.GetByIdAsync(id);
            
            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found", id);
                return NotFound($"Product with ID {id} not found");
            }
            
            return Ok(product);
        }


        /// <summary>
        /// Filter products by category
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>List of products in the specified category</returns>
        [HttpGet("category/{category}")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProductsByCategory(string category)
        {
            _logger.LogInformation("Getting products in category: {Category}", category);
            
            var categoryProducts = await _productService.GetByCategoryAsync(category);
            
            return Ok(categoryProducts);
        }

        /// <summary>
        /// Get product statistics
        /// </summary>
        /// <returns>Statistics about the product catalog</returns>
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetProductStats()
        {
            _logger.LogInformation("Getting product statistics");
            
            var stats = await _productService.GetStatsAsync();
            
            return Ok(stats);
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        /// <returns>API health status</returns>
        [HttpGet("health")]
        public ActionResult<object> Health()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            });
        }

        /// <summary>
        /// Create a new product
        /// </summary>
        /// <param name="request">Product creation request</param>
        /// <returns>Created product with 201 status</returns>
        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductRequest request)
        {
            _logger.LogInformation("Creating new product: {Title}", request.Title);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for product creation: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))));
                return BadRequest(ModelState);
            }

            try
            {
                var product = await _productService.CreateAsync(request);
                _logger.LogInformation("Successfully created product with ID: {ProductId}", product.Id);

                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {Title}", request.Title);
                return StatusCode(500, "An error occurred while creating the product");
            }
        }

        /// <summary>
        /// Create multiple products in bulk
        /// </summary>
        /// <param name="request">Bulk creation request</param>
        /// <returns>List of created products</returns>
        [HttpPost("bulk")]
        public async Task<ActionResult<List<Product>>> CreateProductsBulk([FromBody] BulkCreateRequest request)
        {
            _logger.LogInformation("Creating {Count} products in bulk", request.Products.Count);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var products = await _productService.CreateBulkAsync(request);
                _logger.LogInformation("Successfully created {Count} products in bulk", products.Count);

                return Ok(new 
                { 
                    Message = $"Successfully created {products.Count} products",
                    Products = products,
                    Count = products.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating products in bulk");
                return StatusCode(500, "An error occurred while creating products");
            }
        }

        /// <summary>
        /// Update an existing product
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <param name="request">Update request</param>
        /// <returns>Updated product or 404 if not found</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<Product>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
        {
            _logger.LogInformation("Updating product with ID: {ProductId}", id);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var product = await _productService.UpdateAsync(id, request);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found for update", id);
                    return NotFound($"Product with ID {id} not found");
                }

                _logger.LogInformation("Successfully updated product with ID: {ProductId}", id);
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID: {ProductId}", id);
                return StatusCode(500, "An error occurred while updating the product");
            }
        }

        /// <summary>
        /// Delete a product (soft delete)
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>204 No Content if successful, 404 if not found</returns>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProduct(int id)
        {
            _logger.LogInformation("Deleting product with ID: {ProductId}", id);

            try
            {
                var deleted = await _productService.DeleteAsync(id);

                if (!deleted)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found for deletion", id);
                    return NotFound($"Product with ID {id} not found");
                }

                _logger.LogInformation("Successfully deleted product with ID: {ProductId}", id);
                return NoContent(); // 204 No Content
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product with ID: {ProductId}", id);
                return StatusCode(500, "An error occurred while deleting the product");
            }
        }
    }
}