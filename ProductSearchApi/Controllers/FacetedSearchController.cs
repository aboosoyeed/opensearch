using Microsoft.AspNetCore.Mvc;
using OpenSearch.Client;
using OpenSearch.Net;
using ProductSearchApi.DTOs;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacetedSearchController : ControllerBase
    {
        private readonly ILogger<FacetedSearchController> _logger;
        private readonly IOpenSearchClient _client;
        private const string IndexName = "products";

        public FacetedSearchController(
            ILogger<FacetedSearchController> logger,
            IConfiguration configuration)
        {
            _logger = logger;

            // Create direct OpenSearch client for faceted search operations
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
        /// Core Requirement 3: Guided Navigation (Faceted Search)
        /// Return available filters and counts for current search result set
        /// </summary>
        [HttpPost("")]
        public async Task<ActionResult<object>> GetFacets([FromBody] FacetedSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Getting facets for query: {Query}", request.Query ?? "all");

                var searchResponse = await _client.SearchAsync<Product>(s => s
                    .Index(IndexName)
                    .Size(request.Size ?? 10)
                    .Query(q => BuildSearchQuery(request))
                    .Aggregations(aggs => aggs
                        // Category facet
                        .Terms("categories", t => t
                            .Field(f => f.Category)
                            .Size(20)
                            .Order(o => o.CountDescending())
                        )
                        // Brand facet
                        .Terms("brands", t => t
                            .Field(f => f.Brand)
                            .Size(15)
                            .Order(o => o.CountDescending())
                        )
                        // Price range facet
                        .Range("price_ranges", r => r
                            .Field(f => f.Price)
                            .Ranges(
                                range => range.To(50),
                                range => range.From(50).To(100),
                                range => range.From(100).To(250),
                                range => range.From(250).To(500),
                                range => range.From(500).To(1000),
                                range => range.From(1000)
                            )
                        )
                    )
                );

                if (!searchResponse.IsValid)
                {
                    _logger.LogError("Faceted search failed: {Error}", searchResponse.DebugInformation);
                    return StatusCode(500, new { Error = "Faceted search failed", Details = searchResponse.DebugInformation });
                }

                var facets = ProcessFacets(searchResponse);

                return Ok(new
                {
                    SearchQuery = request.Query ?? "All products",
                    TotalResults = searchResponse.Total,
                    TimeTaken = searchResponse.Took,
                    Results = searchResponse.Documents,
                    Facets = facets,
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

        #region Helper Methods

        private QueryContainer BuildSearchQuery(FacetedSearchRequest request)
        {
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
                    Fields = new[] { "title^2", "description", "category", "brand" },
                    Type = TextQueryType.BestFields,
                    Fuzziness = Fuzziness.Auto
                });
            }

            // Add category filter
            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                filterClauses.Add(new TermQuery { Field = "category", Value = request.Category });
            }

            // Add brand filter
            if (!string.IsNullOrWhiteSpace(request.Brand))
            {
                filterClauses.Add(new TermQuery { Field = "brand", Value = request.Brand });
            }

            // Add price range filter
            if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
            {
                var rangeQuery = new NumericRangeQuery { Field = "price" };
                if (request.MinPrice.HasValue)
                    rangeQuery.GreaterThanOrEqualTo = (double)request.MinPrice.Value;
                if (request.MaxPrice.HasValue)
                    rangeQuery.LessThanOrEqualTo = (double)request.MaxPrice.Value;
                filterClauses.Add(rangeQuery);
            }

            // Build final query
            if (mustClauses.Any() || filterClauses.Any())
            {
                return new BoolQuery
                {
                    Must = mustClauses.Any() ? mustClauses : new QueryContainer[] { new MatchAllQuery() },
                    Filter = filterClauses
                };
            }

            return new MatchAllQuery();
        }

        private object ProcessFacets(ISearchResponse<Product> response)
        {
            var categoriesAgg = response.Aggregations.Terms("categories");
            var brandsAgg = response.Aggregations.Terms("brands");
            var priceRangesAgg = response.Aggregations.Range("price_ranges");

            return new
            {
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
                PriceRanges = priceRangesAgg.Buckets.Select(b => new
                {
                    Range = b.Key,
                    Count = (int)b.DocCount,
                    From = b.From,
                    To = b.To
                }).ToList()
            };
        }

        #endregion
    }
}