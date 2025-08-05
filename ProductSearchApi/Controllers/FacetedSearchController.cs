using Microsoft.AspNetCore.Mvc;
using ProductSearchApi.DTOs;
using ProductSearchApi.Interfaces;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacetedSearchController : ControllerBase
    {
        private readonly ILogger<FacetedSearchController> _logger;
        private readonly ISearchEngine _searchEngine;

        public FacetedSearchController(
            ILogger<FacetedSearchController> logger,
            ISearchEngine searchEngine)
        {
            _logger = logger;
            _searchEngine = searchEngine;
        }

        /// <summary>
        /// Core Requirement 3: Guided Navigation (Faceted Search)
        /// Return available filters and counts for current search result set
        /// </summary>
        [HttpPost("")]
        public async Task<ActionResult<object>> GetFacets([FromBody] FacetedSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Getting facets for query: {Query}", request.Query ?? "all");

                var facetedQuery = new FacetedSearchQuery
                {
                    Query = request.Query,
                    Page = 1,
                    Size = request.Size ?? 10,
                    Filters = new Dictionary<string, object>(),
                    FacetFields = new List<string> { "categories", "brands", "price_ranges" },
                    FacetSizes = new Dictionary<string, int> 
                    { 
                        { "categories", 20 }, 
                        { "brands", 15 } 
                    }
                };

                // Add filters
                if (!string.IsNullOrWhiteSpace(request.Category))
                    facetedQuery.Filters["category"] = request.Category;
                if (!string.IsNullOrWhiteSpace(request.Brand))
                    facetedQuery.Filters["brand"] = request.Brand;
                if (request.MinPrice.HasValue)
                    facetedQuery.Filters["minPrice"] = request.MinPrice.Value;
                if (request.MaxPrice.HasValue)
                    facetedQuery.Filters["maxPrice"] = request.MaxPrice.Value;

                var searchResponse = await _searchEngine.SearchWithFacetsAsync<Product>(facetedQuery);

                return Ok(new
                {
                    SearchQuery = request.Query ?? "All products",
                    TotalResults = searchResponse.Total,
                    TimeTaken = 0, // Search engine abstraction doesn't expose this
                    Results = searchResponse.Documents,
                    Facets = new
                    {
                        Categories = searchResponse.Facets.GetValueOrDefault("categories", new List<FacetBucket>())
                            .Select(b => new { Category = b.Key, Count = (int)b.Count }),
                        Brands = searchResponse.Facets.GetValueOrDefault("brands", new List<FacetBucket>())
                            .Select(b => new { Brand = b.Key, Count = (int)b.Count }),
                        PriceRanges = searchResponse.Facets.GetValueOrDefault("priceRanges", new List<FacetBucket>())
                            .Select(b => new 
                            { 
                                Range = b.Key, 
                                Count = (int)b.Count, 
                                From = b.From, 
                                To = b.To 
                            })
                    },
                    FacetType = "Guided Navigation Facets",
                    Description = "Core Requirement 3: Available filters and counts for current search result set"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting facets");
                return StatusCode(500, new { Error = "An error occurred during faceted search" });
            }
        }

        /// <summary>
        /// GET endpoint for simple facet queries (for easy testing)
        /// </summary>
        [HttpGet("")]
        public async Task<ActionResult<object>> GetFacetsGet(
            [FromQuery] string? query = null,
            [FromQuery] string? category = null,
            [FromQuery] string? brand = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] int size = 10)
        {
            var request = new FacetedSearchRequest
            {
                Query = query,
                Category = category,
                Brand = brand,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Size = size
            };

            return await GetFacets(request);
        }
    }
}