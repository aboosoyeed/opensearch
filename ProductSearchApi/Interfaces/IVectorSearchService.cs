namespace ProductSearchApi.Interfaces
{
    /// <summary>
    /// Interface for vector-based semantic search capabilities
    /// Future implementation will enable similarity search using embeddings
    /// </summary>
    public interface IVectorSearchService
    {
        /// <summary>
        /// Performs semantic search using vector embeddings
        /// </summary>
        /// <param name="query">Natural language query</param>
        /// <param name="top">Number of results to return</param>
        /// <returns>Products ranked by semantic similarity</returns>
        Task<IEnumerable<Product>> SemanticSearchAsync(string query, int top = 10);

        /// <summary>
        /// Finds similar products based on vector similarity
        /// </summary>
        /// <param name="productId">Reference product ID</param>
        /// <param name="top">Number of similar products to return</param>
        /// <returns>Products similar to the reference product</returns>
        Task<IEnumerable<Product>> FindSimilarProductsAsync(int productId, int top = 5);

        /// <summary>
        /// Generates embeddings for a product
        /// </summary>
        /// <param name="product">Product to generate embeddings for</param>
        /// <returns>Vector embedding as float array</returns>
        Task<float[]> GenerateEmbeddingAsync(Product product);

        /// <summary>
        /// Updates product embeddings in the vector index
        /// </summary>
        /// <param name="products">Products to update embeddings for</param>
        Task UpdateEmbeddingsAsync(IEnumerable<Product> products);
    }
}