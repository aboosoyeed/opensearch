namespace ProductSearchApi.Services
{
    public interface IOpenSearchService
    {
        Task<bool> PingAsync();
        Task<bool> CreateIndexAsync();
    }
}