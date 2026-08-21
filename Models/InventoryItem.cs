using System.ComponentModel.DataAnnotations;

namespace FeedMe.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = "/images/default-food.png";

        [Required]
        public int StockQuantity { get; set; }

        // Controlled by Admin: Is this item published on the active student menu?
        public bool IsPublishedToMenu { get; set; } = false;

        // Controlled by Admin: Display on landing page hot section
        public bool IsHotMeal { get; set; } = false;

        public int CategoryId { get; set; }
        public Category? Category { get; set; }
    }
}