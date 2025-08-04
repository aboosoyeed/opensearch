namespace ProductSearchApi.Interfaces
{
    /// <summary>
    /// Simplified search orchestrator for POC - coordinates between traditional and future AI search
    /// </summary>
    public interface ISearchOrchestrator
    {
        /// <summary>
        /// Execute search with option to blend traditional and vector search results
        /// </summary>
        Task<SearchResponse> SearchAsync(SearchRequest request);

        /// <summary>
        /// Get current search configuration (which features are enabled)
        /// </summary>
        Task<SearchConfig> GetConfigurationAsync();
    }

    /// <summary>
    /// Simple search request
    /// </summary>
    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, string> Filters { get; set; } = new();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SortBy { get; set; }
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
    }

    /// <summary>
    /// Simple search response
    /// </summary>
    public class SearchResponse
    {
        public IEnumerable<Product> Results { get; set; } = new List<Product>();
        public int TotalHits { get; set; }
        public string SearchType { get; set; } = "Keyword"; // Keyword, Vector, or Hybrid
    }

    /// <summary>
    /// Simple configuration for POC
    /// </summary>
    public class SearchConfig
    {
        public bool EnableVectorSearch { get; set; } = false;
        
        /// <summary>
        /// Controls the blend between traditional keyword search and AI vector search (0.0 to 1.0)
        /// 0.0 = 100% Traditional Keyword Search (No AI)
        /// 0.5 = 50% Keyword + 50% Vector Search
        /// 1.0 = 100% Vector Search (Pure AI)
        /// 
        /// Example: With VectorSearchWeight = 0.7
        /// Final Score = (0.3 × Keyword Score) + (0.7 × Vector Score)
        /// 
        /// Typical values:
        /// - E-commerce: 0.3-0.5 (balanced approach)
        /// - Content discovery: 0.7-0.9 (AI-heavy)
        /// - Medical/Legal: 0.1-0.2 (precision-focused)
        /// </summary>
        public double VectorSearchWeight { get; set; } = 0.0;
        
        /// <summary>
        /// Vector search algorithm to use
        /// Options: "hnsw" (Hierarchical Navigable Small World), "ivf" (Inverted File Index), "flat" (brute force)
        /// HNSW: Best for high recall, memory intensive
        /// IVF: Good for large datasets, faster but lower recall
        /// Flat: Exact search, slow but 100% accurate
        /// </summary>
        public string VectorAlgorithm { get; set; } = "hnsw";
        
        /// <summary>
        /// Distance metric for vector similarity
        /// Options: "cosine" (angular distance), "l2" (Euclidean), "dot_product" (inner product)
        /// Cosine: Best for normalized embeddings, scale-invariant
        /// L2: Good for absolute distances in vector space
        /// Dot Product: Fast, good for normalized vectors
        /// </summary>
        public string DistanceMetric { get; set; } = "cosine";
    }
}