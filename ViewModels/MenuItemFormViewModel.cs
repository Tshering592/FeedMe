
// ViewModels/MenuItemFormViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace FeedMe.ViewModels
{
    public class MenuItemFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required.")]
        [Range(1, 10000, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Display(Name = "Image URL")]
        public string ImageUrl { get; set; } = "https://via.placeholder.com/150";

        [Display(Name = "Publish to Public Menu")]
        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Mark as Hot Meal (Landing Page Feature)")]
        public bool IsHotMeal { get; set; } = false;

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        public IEnumerable<SelectListItem>? Categories { get; set; }
    }
}