using System;
using System.Collections.Generic;

namespace ProductSearchApi
{
    public class Product
    {
        // Properties with automatic backing fields
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Brand { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }

        // Default constructor
        public Product()
        {
            Attributes = new Dictionary<string, string>();
            CreatedDate = DateTime.UtcNow;
            IsActive = true;
        }

        // Parameterized constructor
        public Product(int id, string title, string description, string category, decimal price, string brand)
        {
            Id = id;
            Title = title;
            Description = description;
            Category = category;
            Price = price;
            Brand = brand;
            Attributes = new Dictionary<string, string>();
            CreatedDate = DateTime.UtcNow;
            IsActive = true;
        }

        // Method to add an attribute
        public void AddAttribute(string key, string value)
        {
            if (Attributes == null)
                Attributes = new Dictionary<string, string>();
            
            Attributes[key] = value;
        }

        // Method to get formatted price
        public string GetFormattedPrice()
        {
            return $"${Price:F2}";
        }

        // Override ToString for better display
        public override string ToString()
        {
            return $"Product: {Title} - {Brand} | {Category} | {GetFormattedPrice()}";
        }

        // Method to validate product data
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Title) &&
                   !string.IsNullOrWhiteSpace(Category) &&
                   !string.IsNullOrWhiteSpace(Brand) &&
                   Price >= 0;
        }
    }
}