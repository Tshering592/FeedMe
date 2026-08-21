using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace FeedMe.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        [NotMapped]
        [Display(Name = "Upload Image")]
        public IFormFile? ImageFile { get; set; }

        public bool IsAvailable { get; set; } = true;

        public bool IsHotMeal { get; set; } = false;

        // Controls whether item is visible on the student menu vs inventory draft
        public bool IsPublished { get; set; } = false;

        [Display(Name = "Preparation Time (Mins)")]
        [Range(0, 120, ErrorMessage = "Preparation time must be between 0 and 120 minutes.")]
        public int PreparationTimeMinutes { get; set; } = 10;

        [Display(Name = "Stock Quantity")]
        [Range(0, 1000, ErrorMessage = "Stock quantity must be non-negative.")]
        public int? StockQuantity { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}