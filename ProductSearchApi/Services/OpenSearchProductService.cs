using ProductSearchApi.DTOs;
using ProductSearchApi.Interfaces;
using System.Text;

namespace ProductSearchApi.Services
{
    /// <summary>
    /// Product service using ISearchEngine abstraction - can work with any search engine
    /// </summary>
    public class OpenSearchProductService : IProductService
    {
        private readonly ISearchEngine _searchEngine;
        private readonly ILogger<OpenSearchProductService> _logger;
        private static int _idCounter = 1;

        public OpenSearchProductService(ISearchEngine searchEngine, ILogger<OpenSearchProductService> logger)
        {
            _searchEngine = searchEngine;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Getting all products from search engine...");
                
                var products = await _searchEngine.GetAllAsync<Product>();
                
                _logger.LogInformation("Returning {Count} products", products.Count());
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting product with ID: {ProductId}", id);
                var product = await _searchEngine.GetDocumentAsync<Product>(id);
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {ProductId}", id);
                return null;
            }
        }

        public async Task<Product> CreateAsync(CreateProductRequest request)
        {
            try
            {
                var product = new Product
                {
                    Id = _idCounter++,
                    Title = request.Title,
                    Description = request.Description,
                    Category = request.Category,
                    Price = request.Price,
                    Brand = request.Brand,
                    Attributes = request.Attributes ?? new Dictionary<string, string>(),
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                var result = await _searchEngine.IndexDocumentAsync(product, product.Id);
                
                if (!result.Success)
                {
                    _logger.LogError("Failed to create product: {Error}", result.Error);
                    throw new Exception("Failed to create product");
                }

                _logger.LogInformation("Created product with ID: {ProductId}", product.Id);
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                throw;
            }
        }

        public async Task<List<Product>> CreateBulkAsync(BulkCreateRequest request)
        {
            try
            {
                _logger.LogInformation("Creating {Count} products in bulk", request.Products.Count);

                var products = request.Products.Select(p => new Product
                {
                    Id = _idCounter++,
                    Title = p.Title,
                    Description = p.Description,
                    Category = p.Category,
                    Price = p.Price,
                    Brand = p.Brand,
                    Attributes = p.Attributes ?? new Dictionary<string, string>(),
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                }).ToList();

                var result = await _searchEngine.BulkIndexDocumentsAsync(products);

                if (!result.Success)
                {
                    _logger.LogError("Bulk creation had errors: {Errors}", string.Join(", ", result.Errors));
                    if (result.ItemsIndexed == 0)
                    {
                        throw new Exception("Failed to create any products");
                    }
                }

                _logger.LogInformation("Successfully created {Count} products", result.ItemsIndexed);
                return products.Take(result.ItemsIndexed).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating products in bulk");
                throw;
            }
        }

        public async Task<Product?> UpdateAsync(int id, UpdateProductRequest request)
        {
            try
            {
                var existingProduct = await GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return null;
                }

                // Update properties
                existingProduct.Title = request.Title ?? existingProduct.Title;
                existingProduct.Description = request.Description ?? existingProduct.Description;
                existingProduct.Category = request.Category ?? existingProduct.Category;
                existingProduct.Price = request.Price ?? existingProduct.Price;
                existingProduct.Brand = request.Brand ?? existingProduct.Brand;
                
                if (request.Attributes != null)
                {
                    existingProduct.Attributes = request.Attributes;
                }

                var result = await _searchEngine.IndexDocumentAsync(existingProduct, id);
                
                if (!result.Success)
                {
                    _logger.LogError("Failed to update product: {Error}", result.Error);
                    throw new Exception("Failed to update product");
                }

                _logger.LogInformation("Updated product with ID: {ProductId}", id);
                return existingProduct;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting product with ID: {ProductId}", id);
                
                var result = await _searchEngine.DeleteDocumentAsync(id);
                
                if (result)
                {
                    _logger.LogInformation("Deleted product with ID: {ProductId}", id);
                }
                else
                {
                    _logger.LogWarning("Failed to delete product with ID: {ProductId}", id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                return false;
            }
        }

        public async Task<IEnumerable<Product>> SearchAsync(string query)
        {
            try
            {
                _logger.LogInformation("Searching products with query: {Query}", query);

                var searchQuery = new SearchQuery
                {
                    Query = query,
                    Page = 1,
                    Size = 100
                };

                var result = await _searchEngine.SearchAsync<Product>(searchQuery);
                return result.Documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
        {
            try
            {
                _logger.LogInformation("Getting products by category: {Category}", category);

                var searchQuery = new SearchQuery
                {
                    Filters = new Dictionary<string, object> { { "category", category } },
                    Page = 1,
                    Size = 100
                };

                var result = await _searchEngine.SearchAsync<Product>(searchQuery);
                return result.Documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by category");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<object> GetStatsAsync()
        {
            try
            {
                var allProducts = await GetAllAsync();
                var productsList = allProducts.ToList();

                var stats = new
                {
                    TotalProducts = productsList.Count,
                    Categories = productsList.GroupBy(p => p.Category)
                        .Select(g => new { Category = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count),
                    Brands = productsList.GroupBy(p => p.Brand)
                        .Select(g => new { Brand = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count),
                    PriceRange = new
                    {
                        Min = productsList.Any() ? productsList.Min(p => p.Price) : 0,
                        Max = productsList.Any() ? productsList.Max(p => p.Price) : 0,
                        Average = productsList.Any() ? productsList.Average(p => p.Price) : 0
                    }
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product stats");
                throw;
            }
        }
    }
}