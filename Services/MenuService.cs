using FeedMe.Data;
using FeedMe.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedMe.Services
{
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _context;

        public MenuService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetCategoriesAsync(bool includeItems = false)
        {
            var query = _context.Categories.AsQueryable();
            if (includeItems)
                query = query.Include(c => c.MenuItems.Where(m => m.IsAvailable));
            return await query.OrderBy(c => c.DisplayOrder).ToListAsync();
        }

        public async Task<List<MenuItem>> GetAvailableItemsAsync(int? categoryId = null)
        {
            var query = _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsAvailable);
            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);
            return await query.ToListAsync();
        }

        public async Task<MenuItem> GetMenuItemByIdAsync(int id)
        {
            return await _context.MenuItems
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task AddMenuItemAsync(MenuItem item)
        {
            _context.MenuItems.Add(item);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateMenuItemAsync(MenuItem item)
        {
            _context.MenuItems.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMenuItemAsync(int id)
        {
            var item = await GetMenuItemByIdAsync(id);
            if (item != null)
            {
                _context.MenuItems.Remove(item);
                await _context.SaveChangesAsync();
            }
        }
    }
}
