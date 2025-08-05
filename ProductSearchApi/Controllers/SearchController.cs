using Microsoft.AspNetCore.Mvc;
using ProductSearchApi.Services;
using ProductSearchApi.DTOs;
using ProductSearchApi.Interfaces;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;
        private readonly ISearchEngine _searchEngine;

        public SearchController(
            ILogger<SearchController> logger,
            ISearchEngine searchEngine)
        {
            _logger = logger;
            _searchEngine = searchEngine;
        }

        /// <summary>
        /// Core Requirement 2: Search API
        /// Full-text search over title and description with filtering, sorting, and pagination
        /// </summary>
        [HttpPost("")]
        public async Task<ActionResult<object>> Search([FromBody] AdvancedSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Executing search with query: {Query}", request.Query);

                var searchQuery = new SearchQuery
                {
                    Query = request.Query,
                    Filters = new Dictionary<string, object>(),
                    SortBy = MapSortField(request.Sort),
                    SortDescending = request.Sort?.EndsWith("_desc") ?? false,
                    Page = request.Page ?? 1,
                    Size = request.Size ?? 10
                };

                // Add filters
                if (!string.IsNullOrWhiteSpace(request.Filters?.Category))
                    searchQuery.Filters["category"] = request.Filters.Category;
                if (!string.IsNullOrWhiteSpace(request.Filters?.Brand))
                    searchQuery.Filters["brand"] = request.Filters.Brand;
                if (request.Filters?.MinPrice.HasValue == true)
                    searchQuery.Filters["minPrice"] = request.Filters.MinPrice.Value;
                if (request.Filters?.MaxPrice.HasValue == true)
                    searchQuery.Filters["maxPrice"] = request.Filters.MaxPrice.Value;

                var searchResponse = await _searchEngine.SearchAsync<Product>(searchQuery);

                return Ok(new
                {
                    Query = request.Query,
                    Filters = request.Filters,
                    Sort = request.Sort,
                    Pagination = new { Page = searchQuery.Page, Size = searchQuery.Size },
                    Results = new
                    {
                        Total = searchResponse.Total,
                        Page = searchQuery.Page,
                        PageSize = searchQuery.Size,
                        TotalPages = (int)Math.Ceiling((double)searchResponse.Total / searchQuery.Size),
                        Products = searchResponse.Hits.Select(h => new
                        {
                            h.Source.Id,
                            h.Source.Title,
                            h.Source.Description,
                            h.Source.Category,
                            h.Source.Brand,
                            h.Source.Price,
                            Score = h.Score
                        })
                    },
                    SearchType = "Advanced Search with Filtering, Sorting, and Pagination",
                    Description = "Core Requirement 2: Full-text search with category/brand/price filtering, sorting by relevance/price, pagination support"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in search");
                return StatusCode(500, new { Error = "An error occurred during search" });
            }
        }

        /// <summary>
        /// GET endpoint for simple search queries (for easy testing)
        /// </summary>
        [HttpGet("")]
        public async Task<ActionResult<object>> SearchGet(
            [FromQuery] string? query = null,
            [FromQuery] string? category = null,
            [FromQuery] string? brand = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sort = "relevance",
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var request = new AdvancedSearchRequest
            {
                Query = query,
                Filters = new SearchFilters
                {
                    Category = category,
                    Brand = brand,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice
                },
                Sort = sort,
                Page = page,
                Size = size
            };

            return await Search(request);
        }

        #region Helper Methods

        private string? MapSortField(string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return "relevance";

            return sort.ToLower() switch
            {
                "price_asc" or "price_desc" => "price",
                "relevance" => "relevance",
                _ => "relevance"
            };
        }

        #endregion
    }
}