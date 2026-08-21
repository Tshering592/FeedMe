using FeedMe.Models;

namespace FeedMe.Services
{
    public interface IMenuService
    {
        Task<List<Category>> GetCategoriesAsync(bool includeItems = false);
        Task<List<MenuItem>> GetAvailableItemsAsync(int? categoryId = null);
        Task<MenuItem> GetMenuItemByIdAsync(int id);
        Task AddMenuItemAsync(MenuItem item);
        Task UpdateMenuItemAsync(MenuItem item);
        Task DeleteMenuItemAsync(int id);
    }
}
