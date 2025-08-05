using Microsoft.AspNetCore.Mvc;
using ProductSearchApi.Services;
using ProductSearchApi.Interfaces;

namespace ProductSearchApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchSuggestionsController : ControllerBase
    {
        private readonly IOpenSearchService _openSearchService;
        private readonly ILogger<SearchSuggestionsController> _logger;
        private readonly ISearchEngine _searchEngine;

        public SearchSuggestionsController(
            IOpenSearchService openSearchService,
            ILogger<SearchSuggestionsController> logger,
            ISearchEngine searchEngine)
        {
            _openSearchService = openSearchService;
            _logger = logger;
            _searchEngine = searchEngine;
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

                var suggestions = await _searchEngine.GetSuggestionsAsync(query, size);

                return Ok(new
                {
                    Query = query,
                    Suggestions = suggestions.Select(s => new
                    {
                        Text = s.Text,
                        Type = s.Type,
                        Category = s.Metadata.GetValueOrDefault("category", ""),
                        Brand = s.Metadata.GetValueOrDefault("brand", ""),
                        ProductCount = s.Metadata.GetValueOrDefault("productCount", 0)
                    }),
                    TotalSuggestions = suggestions.Count(),
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
    }
}