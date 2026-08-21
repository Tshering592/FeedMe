using FeedMe.Models;

namespace FeedMe.ViewModels
{
    public class UserWithRoleViewModel
    {
        public AppUser User { get; set; } = default!;
        public string Role { get; set; } = string.Empty;
    }
}