using Microsoft.AspNetCore.Mvc;
using OpenSearch.Client;
using OpenSearch.Net;
using ProductSearchApi.Services;
using ProductSearchApi.DTOs;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ILogger<SearchController> _logger;
        private readonly IOpenSearchClient _client;
        private const string IndexName = "products";

        public SearchController(
            ILogger<SearchController> logger,
            IConfiguration configuration)
        {
            _logger = logger;

            // Create direct OpenSearch client for search operations
            var opensearchUrl = configuration["OpenSearch:Url"] ?? "http://192.168.64.2:9200";
            var settings = new ConnectionSettings(new Uri(opensearchUrl))
                .DefaultIndex(IndexName)
                .EnableDebugMode()
                .PrettyJson()
                .ThrowExceptions(false);
            _client = new OpenSearchClient(settings);
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

                var searchRequest = BuildSearchQuery(request);
                var searchResponse = await _client.SearchAsync<Product>(searchRequest);

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Search failed: {Error}", searchResponse.DebugInformation);
                    return StatusCode(500, new { Error = "Search failed", Details = searchResponse.DebugInformation });
                }

                return Ok(new
                {
                    Query = request.Query,
                    Filters = request.Filters,
                    Sort = request.Sort,
                    Pagination = new { Page = request.Page, Size = request.Size },
                    Results = new
                    {
                        Total = searchResponse.Total,
                        Page = request.Page ?? 1,
                        PageSize = request.Size ?? 10,
                        TotalPages = (int)Math.Ceiling((double)(searchResponse.Total) / (request.Size ?? 10)),
                        Products = searchResponse.Documents.Select(p => new
                        {
                            p.Id,
                            p.Title,
                            p.Description,
                            p.Category,
                            p.Brand,
                            p.Price,
                            Score = searchResponse.HitsMetadata?.Hits?.FirstOrDefault(h => h.Source?.Id == p.Id)?.Score
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

        private Func<SearchDescriptor<Product>, ISearchRequest> BuildSearchQuery(AdvancedSearchRequest request)
        {
            return s =>
            {
                var searchDescriptor = s.Index(IndexName);

                // Build query with filters
                var mustClauses = new List<QueryContainer>();
                var filterClauses = new List<QueryContainer>();

                // Always filter for active products
                filterClauses.Add(new TermQuery { Field = "isActive", Value = true });

                // Add text search if provided
                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    mustClauses.Add(new MultiMatchQuery
                    {
                        Query = request.Query,
                        Fields = new[] { "title^2", "description" }, // Title has 2x boost
                        Type = TextQueryType.BestFields,
                        Fuzziness = Fuzziness.Auto
                    });
                }

                // Add category filter
                if (!string.IsNullOrWhiteSpace(request.Filters?.Category))
                {
                    filterClauses.Add(new TermQuery { Field = "category", Value = request.Filters.Category });
                }

                // Add brand filter
                if (!string.IsNullOrWhiteSpace(request.Filters?.Brand))
                {
                    filterClauses.Add(new TermQuery { Field = "brand", Value = request.Filters.Brand });
                }

                // Add price range filter
                if (request.Filters?.MinPrice.HasValue == true || request.Filters?.MaxPrice.HasValue == true)
                {
                    var rangeQuery = new NumericRangeQuery { Field = "price" };
                    if (request.Filters.MinPrice.HasValue)
                        rangeQuery.GreaterThanOrEqualTo = (double)request.Filters.MinPrice.Value;
                    if (request.Filters.MaxPrice.HasValue)
                        rangeQuery.LessThanOrEqualTo = (double)request.Filters.MaxPrice.Value;
                    filterClauses.Add(rangeQuery);
                }

                // Build final query
                if (mustClauses.Any() || filterClauses.Any())
                {
                    searchDescriptor = searchDescriptor.Query(q => new BoolQuery
                    {
                        Must = mustClauses.Any() ? mustClauses : new QueryContainer[] { new MatchAllQuery() },
                        Filter = filterClauses
                    });
                }
                else
                {
                    searchDescriptor = searchDescriptor.Query(q => q.MatchAll());
                }

                // Add sorting
                if (!string.IsNullOrWhiteSpace(request.Sort))
                {
                    switch (request.Sort.ToLower())
                    {
                        case "price_asc":
                            searchDescriptor = searchDescriptor.Sort(sort => sort.Ascending(p => p.Price));
                            break;
                        case "price_desc":
                            searchDescriptor = searchDescriptor.Sort(sort => sort.Descending(p => p.Price));
                            break;
                        case "relevance":
                        default:
                            searchDescriptor = searchDescriptor.Sort(sort => sort.Descending(SortSpecialField.Score));
                            break;
                    }
                }

                // Add pagination
                var page = request.Page ?? 1;
                var size = request.Size ?? 10;
                searchDescriptor = searchDescriptor.From((page - 1) * size).Size(size);

                return searchDescriptor;
            };
        }

        #endregion
    }
}