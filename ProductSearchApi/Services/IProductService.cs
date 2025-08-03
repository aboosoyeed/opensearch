using ProductSearchApi.DTOs;

namespace ProductSearchApi.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<Product> CreateAsync(CreateProductRequest request);
        Task<List<Product>> CreateBulkAsync(BulkCreateRequest request);
        Task<Product?> UpdateAsync(int id, UpdateProductRequest request);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<Product>> SearchAsync(string query);
        Task<IEnumerable<Product>> GetByCategoryAsync(string category);
        Task<object> GetStatsAsync();
    }
}