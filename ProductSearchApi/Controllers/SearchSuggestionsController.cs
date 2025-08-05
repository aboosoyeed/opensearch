using Microsoft.AspNetCore.Mvc;
using OpenSearch.Client;
using ProductSearchApi.Services;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchSuggestionsController : ControllerBase
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly ILogger<SearchSuggestionsController> _logger;
        private readonly IOpenSearchClient _client;
        private const string IndexName = "products";

        public SearchSuggestionsController(
            IOpenSearchService openSearchService,
            ILogger<SearchSuggestionsController> logger,
            IConfiguration configuration)
        {
            _openSearchService = openSearchService;
            _logger = logger;

            // Create direct OpenSearch client for search suggestions
            var opensearchUrl = Environment.GetEnvironmentVariable("OPENSEARCH_URL") 
                ?? configuration["OpenSearch:Url"] 
                ?? "http://localhost:9200";
            var settings = new ConnectionSettings(new Uri(opensearchUrl))
                .DefaultIndex(IndexName)
                .EnableDebugMode()
                .PrettyJson()
                .ThrowExceptions(false);
            _client = new OpenSearchClient(settings);
        }

        /// <summary>
        /// Get autocomplete suggestions based on search input prefix - Core Requirement 4
        /// </summary>
        [HttpGet("complete")]
        public async Task<ActionResult<object>> GetCompletionSuggestions(
            [FromQuery] string query,
            [FromQuery] int size = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { Error = "Query parameter is required" });
            }

            try
            {
                _logger.LogInformation("Getting autocomplete suggestions for query: {Query}", query);

                // Simple prefix search across product data
                var searchResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(size * 3) // Get more results to extract unique suggestions
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                sh => sh.Prefix(p => p.Field(f => f.Title).Value(query).Boost(3)),
                                sh => sh.Prefix(p => p.Field(f => f.Brand).Value(query).Boost(2)),
                                sh => sh.Prefix(p => p.Field(f => f.Category).Value(query).Boost(1))
                            )
                        )
                    )
                );

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Autocomplete suggestions failed: {Error}", searchResponse.DebugInformation);
                    return StatusCode(500, new { Error = "Autocomplete suggestions failed", Details = searchResponse.DebugInformation });
                }

                var suggestions = ProcessAutocompleteSuggestions(searchResponse, query, size);

                return Ok(new
                {
                    Query = query,
                    Suggestions = suggestions,
                    TotalSuggestions = suggestions.Count,
                    SuggesterType = "Autocomplete Suggestions",
                    Description = "Core Requirement 4: Autocomplete suggestions based on search input prefix"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting autocomplete suggestions");
                return StatusCode(500, new { Error = "An error occurred getting autocomplete suggestions" });
            }
        }



        #region Helper Methods

        private List<object> ProcessAutocompleteSuggestions(ISearchResponse<Product> response, string query, int size)
        {
            var suggestions = new List<object>();
            var uniqueSuggestions = new HashSet<string>();

            foreach (var product in response.Documents)
            {
                // Extract suggestions from title
                if (!string.IsNullOrWhiteSpace(product.Title) && 
                    product.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (uniqueSuggestions.Add(product.Title.ToLower()))
                    {
                        suggestions.Add(new
                        {
                            Text = product.Title,
                            Type = "Product",
                            Category = product.Category,
                            Brand = product.Brand
                        });
                    }
                }

                // Extract suggestions from brand
                if (!string.IsNullOrWhiteSpace(product.Brand) && 
                    product.Brand.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (uniqueSuggestions.Add(product.Brand.ToLower()))
                    {
                        suggestions.Add(new
                        {
                            Text = product.Brand,
                            Type = "Brand",
                            ProductCount = response.Documents.Count(p => p.Brand == product.Brand)
                        });
                    }
                }

                // Extract suggestions from category
                if (!string.IsNullOrWhiteSpace(product.Category) && 
                    product.Category.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    if (uniqueSuggestions.Add(product.Category.ToLower()))
                    {
                        suggestions.Add(new
                        {
                            Text = product.Category,
                            Type = "Category",
                            ProductCount = response.Documents.Count(p => p.Category == product.Category)
                        });
                    }
                }

                if (suggestions.Count >= size) break;
            }

            return suggestions.Take(size).ToList();
        }

        #endregion
    }
}