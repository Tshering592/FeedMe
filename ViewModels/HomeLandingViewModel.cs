using FeedMe.Models;

namespace FeedMe.ViewModels
{
    public class HomeLandingViewModel
    {
        public List<MenuItem> HotMeals { get; set; } = new();
        public List<MenuItem> MenuItems { get; set; } = new();
    }
}