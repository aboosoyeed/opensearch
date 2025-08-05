using ProductSearchApi.DTOs;

namespace ProductSearchApi.Interfaces
{
    /// <summary>
    /// Abstraction layer for search engine operations
    /// Allows switching between OpenSearch, Elasticsearch, or other search engines
    /// </summary>
    public interface ISearchEngine
    {
        /// <summary>
        /// Index a single document
        /// </summary>
        Task<IndexResult> IndexDocumentAsync<T>(T document, int? id = null) where T : class;

        /// <summary>
        /// Bulk index multiple documents
        /// </summary>
        Task<BulkIndexResult> BulkIndexDocumentsAsync<T>(IEnumerable<T> documents) where T : class;

        /// <summary>
        /// Get document by ID
        /// </summary>
        Task<T?> GetDocumentAsync<T>(int id) where T : class;

        /// <summary>
        /// Delete document by ID
        /// </summary>
        Task<bool> DeleteDocumentAsync(int id);

        /// <summary>
        /// Search documents with query and filters
        /// </summary>
        Task<SearchResult<T>> SearchAsync<T>(SearchQuery query) where T : class;

        /// <summary>
        /// Get all documents
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync<T>() where T : class;

        /// <summary>
        /// Get search suggestions
        /// </summary>
        Task<IEnumerable<SearchSuggestion>> GetSuggestionsAsync(string prefix, int size = 5);

        /// <summary>
        /// Search with facets/aggregations
        /// </summary>
        Task<FacetedSearchResult<T>> SearchWithFacetsAsync<T>(FacetedSearchQuery query) where T : class;

        /// <summary>
        /// Check if search engine is healthy
        /// </summary>
        Task<bool> IsHealthyAsync();
    }

    #region DTOs for Search Engine abstraction

    public class IndexResult
    {
        public bool Success { get; set; }
        public string? Id { get; set; }
        public string? Error { get; set; }
    }

    public class BulkIndexResult
    {
        public bool Success { get; set; }
        public int ItemsIndexed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class SearchQuery
    {
        public string? Query { get; set; }
        public Dictionary<string, object> Filters { get; set; } = new();
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 10;
    }

    public class SearchResult<T>
    {
        public IEnumerable<T> Documents { get; set; } = new List<T>();
        public long Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
        public List<SearchHit<T>> Hits { get; set; } = new();
    }

    public class SearchHit<T>
    {
        public T Source { get; set; } = default!;
        public double? Score { get; set; }
    }

    public class SearchSuggestion
    {
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class FacetedSearchQuery : SearchQuery
    {
        public List<string> FacetFields { get; set; } = new();
        public Dictionary<string, int> FacetSizes { get; set; } = new();
    }

    public class FacetedSearchResult<T> : SearchResult<T>
    {
        public Dictionary<string, List<FacetBucket>> Facets { get; set; } = new();
    }

    public class FacetBucket
    {
        public string Key { get; set; } = string.Empty;
        public long Count { get; set; }
        public double? From { get; set; }
        public double? To { get; set; }
    }

    #endregion
}