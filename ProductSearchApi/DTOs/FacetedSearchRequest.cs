namespace ProductSearchApi.DTOs
{
    /// <summary>
    /// DTO for faceted search request - Core Requirement 3
    /// </summary>
    public class FacetedSearchRequest
    {
        public string? Query { get; set; }
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? Size { get; set; } = 10;
    }
}