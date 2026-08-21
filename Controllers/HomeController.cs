using FeedMe.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace FeedMe.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: / (Landing Page)
        public async Task<IActionResult> Index()
        {
            // 1. Fetch Hot Meals for the Blue Banner Slideshow
            var hotMeals = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsPublished && m.IsAvailable && m.IsHotMeal)
                .ToListAsync();

            // 2. Fetch maximum of 6 items for the landing page grid
            var landingGridItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsPublished && m.IsAvailable)
                .Take(6)
                .ToListAsync();

            ViewBag.HotMeals = hotMeals;

            return View(landingGridItems);
        }

        // GET: /Home/Menu (Public Full Menu Page)
        public async Task<IActionResult> Menu(int? categoryId, string? search)
        {
            var query = _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsPublished && m.IsAvailable)
                .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(m => m.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.Name.Contains(search) || (m.Description != null && m.Description.Contains(search)));
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SelectedCategory = categoryId;
            ViewBag.SearchQuery = search;

            var menuItems = await query.ToListAsync();
            return View(menuItems);
        }
    }
}