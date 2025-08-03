using System.ComponentModel.DataAnnotations;

namespace ProductSearchApi.DTOs
{
    /// <summary>
    /// DTO for advanced search request with filtering, sorting, and pagination
    /// </summary>
    public class AdvancedSearchRequest
    {
        public string? Query { get; set; }
        public SearchFilters? Filters { get; set; }
        public string? Sort { get; set; } = "relevance"; // relevance, price_asc, price_desc
        public int? Page { get; set; } = 1;
        public int? Size { get; set; } = 10;
    }

    /// <summary>
    /// Filters for advanced search
    /// </summary>
    public class SearchFilters
    {
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
    }
}