using System.ComponentModel.DataAnnotations;

namespace ProductSearchApi.DTOs
{
    public class BulkCreateRequest
    {
        [Required(ErrorMessage = "Products list is required")]
        [MinLength(1, ErrorMessage = "At least one product is required")]
        [MaxLength(100, ErrorMessage = "Cannot create more than 100 products at once")]
        public List<CreateProductRequest> Products { get; set; } = new List<CreateProductRequest>();
    }
}