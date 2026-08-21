using System.ComponentModel.DataAnnotations;

namespace FeedMe.ViewModels
{
    public class InventoryItemCreateViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 10000.00)]
        public decimal Price { get; set; }

        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0, 10000)]
        public int StockQuantity { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public IFormFile? ImageFile { get; set; }
    }
}