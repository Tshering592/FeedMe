using System.ComponentModel.DataAnnotations;

namespace FeedMe.ViewModels
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? ContactNumber { get; set; }

        public string? DietaryPreference { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}