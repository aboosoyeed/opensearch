using System.ComponentModel.DataAnnotations;

namespace ProductSearchApi.DTOs
{
    public class UpdateProductRequest
    {
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters")]
        public string? Title { get; set; }

        [StringLength(500, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 500 characters")]
        public string? Description { get; set; }

        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
        public string? Category { get; set; }

        [Range(0.01, 999999.99, ErrorMessage = "Price must be between $0.01 and $999,999.99")]
        public decimal? Price { get; set; }

        [StringLength(50, ErrorMessage = "Brand cannot exceed 50 characters")]
        public string? Brand { get; set; }

        public Dictionary<string, string>? Attributes { get; set; }

        public bool? IsActive { get; set; }
    }
}