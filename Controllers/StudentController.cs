using FeedMe.Data;
using FeedMe.Models;
using FeedMe.Services;
using FeedMe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeedMe.Controllers
{
    [Authorize]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly INotificationService _notificationService;

        public StudentController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // GET: /Student/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.User = user;

            ViewBag.FeaturedItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsPublished && m.IsAvailable)
                .Take(4)
                .ToListAsync();

            ViewBag.TimeSlots = await _context.TimeSlots
                .Where(s => s.IsActive)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.StudentId == user.Id || (user.StudentLcid != null && o.StudentLcid == user.StudentLcid))
                .OrderByDescending(o => o.OrderTime)
                .Take(5)
                .ToListAsync();

            ViewBag.Notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id || (user.StudentLcid != null && n.UserId == user.StudentLcid))
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        // GET: /Student/FullMenu
        [HttpGet]
        public async Task<IActionResult> FullMenu(int? categoryId)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.User = user;

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.SelectedCategoryId = categoryId;

            var menuQuery = _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsPublished && m.IsAvailable);

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                menuQuery = menuQuery.Where(m => m.CategoryId == categoryId.Value);
            }

            ViewBag.MenuItems = await menuQuery.ToListAsync();

            ViewBag.TimeSlots = await _context.TimeSlots
                .Where(s => s.IsActive)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return View();
        }

        // POST: /Student/PlaceMultiOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceMultiOrder(CartOrderViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "Your session expired. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            var validItems = model.SelectedItems?.Where(i => i.Quantity > 0).ToList();
            if (validItems == null || !validItems.Any())
            {
                TempData["Error"] = "Please select at least one item with quantity greater than 0.";
                return RedirectToAction(nameof(FullMenu));
            }

            if (model.TimeSlotId <= 0)
            {
                TempData["Error"] = "Please select a valid pickup time slot.";
                return RedirectToAction(nameof(FullMenu));
            }

            decimal grandTotal = 0m;
            var orderItemsList = new List<OrderItem>();

            foreach (var selection in validItems)
            {
                var menuItem = await _context.MenuItems.FindAsync(selection.MenuItemId);
                if (menuItem != null)
                {
                    if (menuItem.StockQuantity.HasValue && menuItem.StockQuantity.Value < selection.Quantity)
                    {
                        TempData["Error"] = $"Insufficient stock for '{menuItem.Name}'. Only {menuItem.StockQuantity.Value} left!";
                        return RedirectToAction(nameof(FullMenu));
                    }

                    decimal itemTotal = menuItem.Price * selection.Quantity;
                    grandTotal += itemTotal;

                    orderItemsList.Add(new OrderItem
                    {
                        MenuItemId = menuItem.Id,
                        Quantity = selection.Quantity,
                        UnitPrice = menuItem.Price
                    });

                    if (menuItem.StockQuantity.HasValue)
                    {
                        menuItem.StockQuantity -= selection.Quantity;
                    }
                }
            }

            // CHECK TOKEN BALANCE
            if (user.TokenBalance < grandTotal)
            {
                TempData["Error"] = $"Insufficient balance! Required: {grandTotal:N0} Tokens. Your Current Balance: {user.TokenBalance:N0} Tokens. Please top up your wallet.";
                return RedirectToAction(nameof(FullMenu));
            }

            // CREATE ORDER RECORD
            var order = new Order
            {
                StudentId = user.Id,
                StudentName = user.FullName ?? user.Email ?? "Student",
                StudentLcid = user.StudentLcid ?? "N/A",
                TimeSlotId = model.TimeSlotId,
                OrderTime = DateTime.Now,
                Status = OrderStatus.Pending,
                TotalAmount = grandTotal,
                OrderItems = orderItemsList
            };

            _context.Orders.Add(order);

            // 1. DEDUCT TOKENS FROM STUDENT WALLET
            user.TokenBalance -= grandTotal;
            await _userManager.UpdateAsync(user);

            // 2. CREDIT TOKENS BACK TO ADMIN VAULT
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminVaultUser = adminUsers.FirstOrDefault();
            if (adminVaultUser != null)
            {
                adminVaultUser.TokenBalance += grandTotal;
                await _userManager.UpdateAsync(adminVaultUser);
            }

            // 3. LOG TOKEN TRANSACTION
            var paymentTransaction = new TokenTransaction
            {
                SenderUserId = user.Id,
                SenderName = user.FullName ?? user.Email ?? "Student",
                ReceiverUserId = adminVaultUser?.Id ?? "AdminVault",
                ReceiverName = "Canteen Central Vault",
                Amount = grandTotal,
                Type = TransactionType.Purchase,
                Description = $"Meal Payment for Order #{order.Id:D3}",
                Timestamp = DateTime.Now
            };

            _context.TokenTransactions.Add(paymentTransaction);
            await _context.SaveChangesAsync();

            // 4. NOTIFY KITCHEN STAFF AND ADMIN (was missing - this is why the Kitchen bell never lit up)
            string notificationMsg = $"New Order #{order.Id:D3} received from {order.StudentName}!";
            await _notificationService.NotifyRoleAsync("Kitchen", notificationMsg, "NewOrder", order.Id);
            await _notificationService.NotifyRoleAsync("Admin", notificationMsg, "NewOrder", order.Id);
            return RedirectToAction(nameof(Orders));
        }

        // GET: /Student/Orders
        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.User = user;
            var orders = await _context.Orders
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.StudentId == user.Id || (user.StudentLcid != null && o.StudentLcid == user.StudentLcid))
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Student/Vault
        [HttpGet]
        public async Task<IActionResult> Vault()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.User = user;

            var userTxs = await _context.TokenTransactions
                .Where(t => t.SenderUserId == user.Id || t.ReceiverUserId == user.Id)
                .ToListAsync();

            ViewBag.TotalReceived = userTxs
                .Where(t => t.ReceiverUserId == user.Id && t.Type == TransactionType.TopUp)
                .Sum(t => t.Amount);

            ViewBag.TotalSpent = userTxs
                .Where(t => t.SenderUserId == user.Id && t.Type == TransactionType.Purchase)
                .Sum(t => t.Amount);

            ViewBag.RecentActivity = userTxs
                .OrderByDescending(t => t.Timestamp)
                .Take(5)
                .ToList();

            return View();
        }

        // GET: /Student/Statements
        [HttpGet]
        public async Task<IActionResult> Statements(string? viewType, int? selectedMonth, int? selectedYear)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.User = user;

            var today = DateTime.Today;
            int currentMonth = selectedMonth ?? today.Month;
            int currentYear = selectedYear ?? today.Year;

            ViewBag.SelectedMonth = currentMonth;
            ViewBag.SelectedYear = currentYear;
            ViewBag.ViewType = viewType ?? "all";

            var query = _context.TokenTransactions
                .Where(t => t.SenderUserId == user.Id || t.ReceiverUserId == user.Id);

            if (viewType == "today")
            {
                query = query.Where(t => t.Timestamp.Date == today);
            }
            else if (viewType == "topup")
            {
                query = query.Where(t => t.ReceiverUserId == user.Id && t.Type == TransactionType.TopUp);
            }
            else if (viewType == "month")
            {
                query = query.Where(t => t.Timestamp.Month == currentMonth && t.Timestamp.Year == currentYear);
            }

            var statementList = await query.OrderByDescending(t => t.Timestamp).ToListAsync();
            return View(statementList);
        }

     
        [HttpGet]
        public async Task<IActionResult> ExportStatementCsv(string? viewType, int? selectedMonth, int? selectedYear)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var today = DateTime.Today;
            int currentMonth = selectedMonth ?? today.Month;
            int currentYear = selectedYear ?? today.Year;

            var query = _context.TokenTransactions
                .Where(t => t.SenderUserId == user.Id || t.ReceiverUserId == user.Id);

            if (viewType == "today")
            {
                query = query.Where(t => t.Timestamp.Date == today);
            }
            else if (viewType == "topup")
            {
                query = query.Where(t => t.ReceiverUserId == user.Id && t.Type == TransactionType.TopUp);
            }
            else if (viewType == "month")
            {
                query = query.Where(t => t.Timestamp.Month == currentMonth && t.Timestamp.Year == currentYear);
            }

            var list = await query.OrderByDescending(t => t.Timestamp).ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Transaction ID,Date & Time,Type,Description,Amount (Tokens),Credit/Debit");

            foreach (var tx in list)
            {
                bool isCredit = tx.ReceiverUserId == user.Id;
                string typeLabel = isCredit ? "Credit / Top-Up" : "Meal Expense";
                string sign = isCredit ? "+" : "-";

                builder.AppendLine($"#TX-{tx.Id:D5},\"{tx.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{typeLabel}\",\"{tx.Description.Replace("\"", "\"\"")}\",{sign}{tx.Amount},{(isCredit ? "CREDIT" : "DEBIT")}");
            }

            string fileName = $"Token_Statement_{user.UserName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", fileName);
        }

        // GET: /Student/Wallet
        [HttpGet]
        public IActionResult Wallet()
        {
            return RedirectToAction(nameof(Vault));
        }

        // GET: /Student/Notifications
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.User = user;

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id || (user.StudentLcid != null && n.UserId == user.StudentLcid))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var unreadList = notifications.Where(n => !n.IsRead).ToList();
            if (unreadList.Any())
            {
                foreach (var notif in unreadList)
                {
                    notif.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return View(notifications);
        }

        // GET: /Student/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(user);
        }

        // POST: /Student/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string contactNumber, string? dietaryPreference)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                user.FullName = fullName;
                user.ContactNumber = contactNumber;
                user.PhoneNumber = contactNumber;
                user.DietaryPreference = dietaryPreference;

                await _userManager.UpdateAsync(user);
                TempData["Success"] = "Profile details updated successfully!";
            }

            return RedirectToAction(nameof(Dashboard));
        }
    }
}