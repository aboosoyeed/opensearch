using OpenSearch.Client;
using OpenSearch.Net;
using ProductSearchApi.DTOs;
using System.Text;

namespace ProductSearchApi.Services
{
    /// <summary>
    /// OpenSearch-based product service - uses OpenSearch as primary data persistence layer
    /// </summary>
    public class OpenSearchProductService : IProductService
    {
        private readonly IOpenSearchClient _client;
        private readonly ILogger<OpenSearchProductService> _logger;
        private const string IndexName = "products";

        public OpenSearchProductService(IOpenSearchClient client, ILogger<OpenSearchProductService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Getting all products from OpenSearch...");
                
                var searchResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(1000) // Get up to 1000 products
                    .Query(q => q.MatchAll()) // Get all products first to debug
                );

                _logger.LogInformation("OpenSearch response: Valid={Valid}, Total={Total}, DocumentsCount={Count}", 
                    searchResponse.IsValid, searchResponse.Total, searchResponse.Documents?.Count() ?? 0);

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Failed to get all products: {Error}", searchResponse.DebugInformation);
                    return Enumerable.Empty<Product>();
                }

                // Filter active products in memory for now
                var activeProducts = searchResponse.Documents.Where(p => p.IsActive).ToList();
                _logger.LogInformation("Returning {Count} active products", activeProducts.Count);
                return activeProducts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products from OpenSearch");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            try
            {
                var getResponse = await _client.GetAsync<Product>(id, g => g.Index(IndexName));

                if (!getResponse.IsValid || !getResponse.Found)
                {
                    return null;
                }

                var product = getResponse.Source;
                return product?.IsActive == true ? product : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by ID {Id} from OpenSearch", id);
                return null;
            }
        }

        public async Task<Product> CreateAsync(CreateProductRequest request)
        {
            try
            {
                // Generate new ID by finding the max existing ID
                var maxIdResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(1)
                    .Query(q => q.MatchAll())
                );

                int newId = 1;
                if (maxIdResponse.IsValid && maxIdResponse.Documents.Any())
                {
                    var maxId = maxIdResponse.Documents.Max(p => p.Id);
                    newId = maxId + 1;
                }

                var product = new Product(newId, request.Title, request.Description, 
                                        request.Category, request.Price, request.Brand);

                if (request.Attributes != null)
                {
                    product.Attributes = new Dictionary<string, string>(request.Attributes);
                }

                var indexResponse = await _client.IndexAsync(product, i => i
                    .Index(IndexName)
                    .Id(product.Id)
                    .Refresh(Refresh.True) // Ensure immediate availability
                );

                if (!indexResponse.IsValid)
                {
                    _logger.LogError("Failed to create product: {Error}", indexResponse.DebugInformation);
                    throw new Exception($"Failed to create product: {indexResponse.DebugInformation}");
                }

                _logger.LogInformation("Created product with ID {Id}", product.Id);
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product in OpenSearch");
                throw;
            }
        }

        public async Task<List<Product>> CreateBulkAsync(BulkCreateRequest request)
        {
            try
            {
                var createdProducts = new List<Product>();

                // Prepare bulk operations
                var bulkDescriptor = new BulkDescriptor();

                foreach (var productRequest in request.Products)
                {
                    // Generate incremental ID for each product  
                    var maxIdResponse = await _client.SearchAsync<Product>(s => s
                        .Index(IndexName)
                        .Size(1)
                        .Query(q => q.MatchAll())
                    );

                    int nextId = createdProducts.Count + 1;
                    if (maxIdResponse.IsValid && maxIdResponse.Documents.Any())
                    {
                        var maxId = maxIdResponse.Documents.Max(p => p.Id);
                        nextId = maxId + createdProducts.Count + 1;
                    }
                    
                    var product = new Product(nextId, productRequest.Title, productRequest.Description,
                                            productRequest.Category, productRequest.Price, productRequest.Brand);

                    if (productRequest.Attributes != null)
                    {
                        product.Attributes = new Dictionary<string, string>(productRequest.Attributes);
                    }

                    bulkDescriptor.Index<Product>(i => i
                        .Index(IndexName)
                        .Id(product.Id)
                        .Document(product)
                    );

                    createdProducts.Add(product);
                }

                var bulkResponse = await _client.BulkAsync(bulkDescriptor.Refresh(Refresh.True));

                if (!bulkResponse.IsValid)
                {
                    _logger.LogError("Bulk create failed: {Error}", bulkResponse.DebugInformation);
                    throw new Exception($"Bulk create failed: {bulkResponse.DebugInformation}");
                }

                if (bulkResponse.Errors)
                {
                    var errorMessages = bulkResponse.ItemsWithErrors.Select(i => i.Error?.Reason).ToArray();
                    _logger.LogWarning("Some bulk operations failed: {Errors}", string.Join(", ", errorMessages));
                }

                _logger.LogInformation("Bulk created {Count} products", createdProducts.Count);
                return createdProducts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk creating products in OpenSearch");
                throw;
            }
        }

        public async Task<Product?> UpdateAsync(int id, UpdateProductRequest request)
        {
            try
            {
                // First check if product exists
                var existingProduct = await GetByIdAsync(id);
                if (existingProduct == null)
                {
                    return null;
                }

                // Create update script
                var updateScript = new StringBuilder("ctx._source.");
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(request.Title))
                {
                    updateScript.Append("title = params.title; ");
                    parameters["title"] = request.Title;
                }

                if (!string.IsNullOrEmpty(request.Description))
                {
                    updateScript.Append("description = params.description; ");
                    parameters["description"] = request.Description;
                }

                if (!string.IsNullOrEmpty(request.Category))
                {
                    updateScript.Append("category = params.category; ");
                    parameters["category"] = request.Category;
                }

                if (request.Price.HasValue)
                {
                    updateScript.Append("price = params.price; ");
                    parameters["price"] = request.Price.Value;
                }

                if (!string.IsNullOrEmpty(request.Brand))
                {
                    updateScript.Append("brand = params.brand; ");
                    parameters["brand"] = request.Brand;
                }

                if (request.Attributes != null)
                {
                    updateScript.Append("attributes = params.attributes; ");
                    parameters["attributes"] = request.Attributes;
                }

                if (request.IsActive.HasValue)
                {
                    updateScript.Append("isActive = params.isActive; ");
                    parameters["isActive"] = request.IsActive.Value;
                }

                var updateResponse = await _client.UpdateAsync<Product>(id, u => u
                    .Index(IndexName)
                    .Script(s => s
                        .Source(updateScript.ToString())
                        .Params(parameters)
                    )
                    .Refresh(Refresh.True)
                );

                if (!updateResponse.IsValid)
                {
                    _logger.LogError("Failed to update product {Id}: {Error}", id, updateResponse.DebugInformation);
                    return null;
                }

                // Return updated product
                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {Id} in OpenSearch", id);
                return null;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                // Soft delete by setting isActive = false
                var updateResponse = await _client.UpdateAsync<Product>(id, u => u
                    .Index(IndexName)
                    .Script(s => s
                        .Source("ctx._source.isActive = false")
                    )
                    .Refresh(Refresh.True)
                );

                if (!updateResponse.IsValid)
                {
                    _logger.LogError("Failed to delete product {Id}: {Error}", id, updateResponse.DebugInformation);
                    return false;
                }

                _logger.LogInformation("Soft deleted product {Id}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {Id} in OpenSearch", id);
                return false;
            }
        }

        public async Task<IEnumerable<Product>> SearchAsync(string query)
        {
            try
            {
                var searchResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(100)
                    .Query(q => q
                        .Bool(b => b
                            .Must(
                                m => m.MultiMatch(mm => mm
                                    .Query(query)
                                    .Fields(f => f
                                        .Field(p => p.Title, 2.0)
                                        .Field(p => p.Description)
                                        .Field(p => p.Category)
                                        .Field(p => p.Brand)
                                    )
                                    .Type(TextQueryType.BestFields)
                                    .Fuzziness(Fuzziness.Auto)
                                ),
                                m => m.Term(t => t.Field("isActive").Value(true))
                            )
                        )
                    )
                    .Sort(sort => sort.Descending(SortSpecialField.Score))
                );

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Search failed: {Error}", searchResponse.DebugInformation);
                    return Enumerable.Empty<Product>();
                }

                return searchResponse.Documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products in OpenSearch");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
        {
            try
            {
                var searchResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(100)
                    .Query(q => q
                        .Bool(b => b
                            .Must(
                                m => m.Term(t => t.Field(f => f.Category).Value(category)),
                                m => m.Term(t => t.Field("isActive").Value(true))
                            )
                        )
                    )
                    .Sort(sort => sort.Ascending(p => p.Id))
                );

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Get by category failed: {Error}", searchResponse.DebugInformation);
                    return Enumerable.Empty<Product>();
                }

                return searchResponse.Documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by category in OpenSearch");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<object> GetStatsAsync()
        {
            try
            {
                var searchResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(0) // We only want aggregations
                    .Query(q => q.MatchAll())
                    .Aggregations(a => a
                        .Filter("active_products", f => f
                            .Filter(ff => ff.Term(t => t.Field("isActive").Value(true)))
                            .Aggregations(aa => aa
                                .Terms("categories", t => t.Field(p => p.Category))
                                .Terms("brands", t => t.Field(p => p.Brand))
                                .Stats("price_stats", st => st.Field(p => p.Price))
                            )
                        )
                        .Filter("deleted_products", f => f
                            .Filter(ff => ff.Term(t => t.Field("isActive").Value(false)))
                        )
                    )
                );

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Get stats failed: {Error}", searchResponse.DebugInformation);
                    return new { Error = "Failed to get statistics" };
                }

                var activeProductsAgg = searchResponse.Aggregations.Filter("active_products");
                var deletedProductsAgg = searchResponse.Aggregations.Filter("deleted_products");
                var categoriesAgg = activeProductsAgg.Terms("categories");
                var brandsAgg = activeProductsAgg.Terms("brands");
                var priceStatsAgg = activeProductsAgg.Stats("price_stats");

                var stats = new
                {
                    TotalProducts = (int)activeProductsAgg.DocCount,
                    TotalDeleted = (int)deletedProductsAgg.DocCount,
                    Categories = categoriesAgg.Buckets.Select(b => new 
                    { 
                        Category = b.Key, 
                        Count = (int)b.DocCount 
                    }).ToList(),
                    Brands = brandsAgg.Buckets.Select(b => new 
                    { 
                        Brand = b.Key, 
                        Count = (int)b.DocCount 
                    }).ToList(),
                    PriceRange = priceStatsAgg.Count > 0 ? new
                    {
                        Min = priceStatsAgg.Min,
                        Max = priceStatsAgg.Max,
                        Average = priceStatsAgg.Average
                    } : null
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats from OpenSearch");
                return new { Error = "Failed to get statistics" };
            }
        }
    }
}