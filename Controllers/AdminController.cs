using FeedMe.Data;
using FeedMe.Hubs;
using FeedMe.Models;
using FeedMe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeedMe.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly UserManager<AppUser> _userManager;

        public AdminController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IHubContext<OrderHub> hubContext,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _hubContext = hubContext;
            _userManager = userManager;
        }

        // 1. GET: /Admin/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // Calculate date range for current week (Sunday - Saturday)
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);

            // Fetch order timestamps for the current week
            var weeklyOrderDays = await _context.Orders
                .Where(o => o.OrderTime >= startOfWeek && o.OrderTime < endOfWeek)
                .Select(o => o.OrderTime.DayOfWeek)
                .ToListAsync();

            // Populate array with order frequencies per day of week
            var weeklyCounts = new int[7];
            foreach (var day in weeklyOrderDays)
            {
                weeklyCounts[(int)day]++;
            }

            var menuItems = await _context.MenuItems
                .Include(m => m.Category)
                .ToListAsync() ?? new List<MenuItem>();

            var pendingOrders = await _context.Orders
                .Include(o => o.Student)
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing)
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync() ?? new List<Order>();

            var todaysOrdersCount = await _context.Orders
                .Where(o => o.OrderTime >= today && o.OrderTime < tomorrow)
                .CountAsync();

            var todaysRevenue = await _context.Orders
                .Where(o => o.OrderTime >= today && o.OrderTime < tomorrow && o.Status != OrderStatus.Cancelled)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            var lowStockItemsCount = await _context.MenuItems
                .Where(m => m.StockQuantity.HasValue && m.StockQuantity.Value <= 5)
                .CountAsync();

            var viewModel = new AdminDashboardViewModel
            {
                MenuItems = menuItems,
                PendingOrders = pendingOrders,
                PendingOrdersCount = pendingOrders.Count,
                TodaysOrdersCount = todaysOrdersCount,
                TodaysRevenue = todaysRevenue,
                LowStockItemsCount = lowStockItemsCount,
                WeeklyOrderCounts = weeklyCounts
            };

            return View(viewModel);
        }

        // 2. GET: /Admin/Inventory
        [HttpGet]
        public async Task<IActionResult> Inventory()
        {
            var menuItems = await _context.MenuItems
                .Include(m => m.Category)
                .ToListAsync();

            return View(menuItems);
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Inventory));
        }

        // 3. GET: /Admin/CreateItem
        [HttpGet]
        public async Task<IActionResult> CreateItem()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View();
        }

        // 4. POST: /Admin/CreateItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateItem(MenuItem model, IFormFile? ImageFile)
        {
            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ImageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    model.ImageUrl = "/images/" + uniqueFileName;
                }

                model.IsPublished = false;

                _context.MenuItems.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Item created in inventory successfully.";
                return RedirectToAction(nameof(Inventory));
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(model);
        }

        // 5. POST: /Admin/DeleteItem/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item != null)
            {
                bool isLinkedToOrders = await _context.OrderItems.AnyAsync(oi => oi.MenuItemId == id);

                if (isLinkedToOrders)
                {
                    item.IsPublished = false;
                    item.IsAvailable = false;
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"'{item.Name}' is linked to past orders and cannot be deleted. It has been unpublished and marked unavailable instead.";
                }
                else
                {
                    _context.MenuItems.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Product deleted successfully from inventory.";
                }
            }

            return RedirectToAction(nameof(Inventory));
        }

        // 6. GET: /Admin/EditItem/5
        [HttpGet]
        public async Task<IActionResult> EditItem(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View("EditMenuItem", item);
        }

        // 7. POST: /Admin/EditItem/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditItem(int id, MenuItem model, IFormFile? ImageFile)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Category");
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingItem = await _context.MenuItems.FindAsync(id);
                    if (existingItem == null)
                    {
                        return NotFound();
                    }

                    existingItem.Name = model.Name;
                    existingItem.Description = model.Description;
                    existingItem.CategoryId = model.CategoryId;
                    existingItem.Price = model.Price;
                    existingItem.StockQuantity = model.StockQuantity;
                    existingItem.IsAvailable = model.IsAvailable;
                    existingItem.IsHotMeal = model.IsHotMeal;

                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                        Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ImageFile.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(fileStream);
                        }

                        existingItem.ImageUrl = "/images/" + uniqueFileName;
                    }

                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Item updated successfully.";
                    return RedirectToAction(nameof(Inventory));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.MenuItems.Any(e => e.Id == model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View("EditMenuItem", model);
        }

        // 8. POST: /Admin/TogglePublish/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item != null)
            {
                item.IsPublished = !item.IsPublished;
                await _context.SaveChangesAsync();
                TempData["Success"] = item.IsPublished ? $"{item.Name} published to live menu." : $"{item.Name} unpublished from menu.";
            }

            string? referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer))
            {
                return Redirect(referer);
            }

            return RedirectToAction(nameof(Inventory));
        }

        // 9. GET: /Admin/Menu
        [HttpGet]
        public async Task<IActionResult> Menu()
        {
            var publishedItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsPublished)
                .ToListAsync();

            ViewBag.UnpublishedItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => !m.IsPublished)
                .ToListAsync();

            return View(publishedItems);
        }

        // 10. GET: /Admin/Orders
        [HttpGet]
        public async Task<IActionResult> Orders(string? statusFilter, string? searchString)
        {
            var query = _context.Orders
                .Include(o => o.Student)
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .AsQueryable();

            if (string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.SelectedFilter = "All";
            }
            else if (string.IsNullOrWhiteSpace(statusFilter) || string.Equals(statusFilter, "Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing);
                ViewBag.SelectedFilter = "Active";
            }
            else if (Enum.TryParse<OrderStatus>(statusFilter, true, out var parsedStatus))
            {
                query = query.Where(o => o.Status == parsedStatus);
                ViewBag.SelectedFilter = statusFilter;
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim().ToLower();
                query = query.Where(o =>
                    o.Id.ToString().Contains(searchString) ||
                    (o.StudentName != null && o.StudentName.ToLower().Contains(searchString)) ||
                    (o.StudentLcid != null && o.StudentLcid.ToLower().Contains(searchString)));
            }

            var orders = await query.OrderByDescending(o => o.OrderTime).ToListAsync();
            ViewBag.SearchString = searchString;

            return View(orders);
        }

        // 11. POST: /Admin/UpdateOrderStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status, string? adminMessage)
        {
            var order = await _context.Orders
                .Include(o => o.Student)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null)
            {
                if (Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
                {
                    var previousStatus = order.Status;

                    if (parsedStatus == OrderStatus.Cancelled && previousStatus != OrderStatus.Cancelled)
                    {
                        var adminUser = await _userManager.GetUserAsync(User);
                        var studentUser = order.Student ?? (order.StudentId != null ? await _userManager.FindByIdAsync(order.StudentId) : null);

                        if (adminUser != null)
                        {
                            adminUser.TokenBalance -= order.TotalAmount;
                            await _userManager.UpdateAsync(adminUser);
                        }

                        if (studentUser != null)
                        {
                            studentUser.TokenBalance += order.TotalAmount;
                            await _userManager.UpdateAsync(studentUser);
                        }

                        if (order.OrderItems != null)
                        {
                            foreach (var item in order.OrderItems)
                            {
                                var menuItem = await _context.MenuItems.FindAsync(item.MenuItemId);
                                if (menuItem != null && menuItem.StockQuantity.HasValue)
                                {
                                    menuItem.StockQuantity += item.Quantity;
                                }
                            }
                        }

                        var refundTx = new TokenTransaction
                        {
                            SenderUserId = adminUser?.Id,
                            SenderName = adminUser?.FullName ?? "Admin Vault",
                            ReceiverUserId = studentUser?.Id,
                            ReceiverName = studentUser?.FullName ?? order.StudentName ?? "User",
                            Amount = order.TotalAmount,
                            Type = TransactionType.TopUp,
                            Description = $"Refund for cancelled Order #{order.Id:D3}",
                            Timestamp = DateTime.Now
                        };

                        _context.TokenTransactions.Add(refundTx);
                    }

                    order.Status = parsedStatus;
                }

                string defaultMsg = $"Your order #{order.Id:D3} is now {order.Status}.";
                string messageToSend = !string.IsNullOrWhiteSpace(adminMessage) ? adminMessage : defaultMsg;
                order.AdminMessage = messageToSend;

                await _context.SaveChangesAsync();

                string targetUserId = order.StudentId ?? order.StudentLcid ?? string.Empty;

                if (!string.IsNullOrEmpty(targetUserId))
                {
                    var notificationData = new
                    {
                        orderId = order.Id,
                        token = $"#{order.Id:D3}",
                        status = order.Status.ToString(),
                        message = messageToSend,
                        updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    await _hubContext.Clients.User(targetUserId).SendAsync("ReceiveOrderStatusUpdate", notificationData);
                    await _hubContext.Clients.Group($"order_{order.Id}").SendAsync("ReceiveOrderStatusUpdate", notificationData);

                    try
                    {
                        var dbNotification = new Notification
                        {
                            UserId = targetUserId,
                            Title = $"Order #{order.Id:D3} - {order.Status}",
                            Message = messageToSend,
                            CreatedAt = DateTime.Now,
                            IsRead = false
                        };
                        _context.Notifications.Add(dbNotification);
                        await _context.SaveChangesAsync();
                    }
                    catch
                    {
                        // Ignore if Notification table schema differs
                    }
                }

                TempData["Success"] = $"Order #{order.Id:D3} status updated to '{status}' and notification sent!";
            }

            return RedirectToAction(nameof(Orders));
        }

        // 12. GET: /Admin/Slots
        [HttpGet]
        public async Task<IActionResult> Slots()
        {
            var timeSlots = await _context.TimeSlots
                .Include(s => s.Orders)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return View(timeSlots);
        }

        [HttpGet]
        public IActionResult CreateSlot()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSlot(TimeSlot model)
        {
            ModelState.Remove("Orders");

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be greater than start time.");
            }

            if (ModelState.IsValid)
            {
                model.IsActive = true;
                _context.TimeSlots.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "New pickup time slot added successfully.";
                return RedirectToAction(nameof(Slots));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSlotStatus(int id)
        {
            var slot = await _context.TimeSlots.FindAsync(id);
            if (slot != null)
            {
                slot.IsActive = !slot.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = slot.IsActive ? "Time slot activated." : "Time slot deactivated.";
            }

            return RedirectToAction(nameof(Slots));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSlot(int id)
        {
            var slot = await _context.TimeSlots.Include(s => s.Orders).FirstOrDefaultAsync(s => s.Id == id);
            if (slot != null)
            {
                if (slot.Orders.Any())
                {
                    slot.IsActive = false;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Slot contains existing orders and was deactivated instead of deleted.";
                }
                else
                {
                    _context.TimeSlots.Remove(slot);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Time slot deleted successfully.";
                }
            }

            return RedirectToAction(nameof(Slots));
        }

        // 13. GET: /Admin/Vault
        [HttpGet]
        public async Task<IActionResult> Vault()
        {
            var adminUser = await _userManager.GetUserAsync(User);
            ViewBag.AdminTokenBalance = adminUser?.TokenBalance ?? 0m;

            var usersQuery = await _userManager.Users.ToListAsync();
            var userList = new List<UserWithRoleViewModel>();

            foreach (var user in usersQuery)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserWithRoleViewModel
                {
                    User = user,
                    Role = roles.FirstOrDefault() ?? "Student"
                });
            }

            ViewBag.RecentTransactions = await _context.TokenTransactions
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .ToListAsync();

            return View(userList);
        }

        // 14. POST: /Admin/DepositToVault
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepositToVault(decimal amount, string securityKey)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Deposit amount must be greater than 0.";
                return RedirectToAction(nameof(Vault));
            }

            var adminUser = await _userManager.GetUserAsync(User);
            if (adminUser == null)
            {
                TempData["Error"] = "Session expired. Please log in again.";
                return RedirectToAction(nameof(Vault));
            }

            bool isPasswordValid = await _userManager.CheckPasswordAsync(adminUser, securityKey);
            bool isMasterKeyValid = securityKey == "TEXAS-VAULT-SECRET-2026";

            if (!isPasswordValid && !isMasterKeyValid)
            {
                TempData["Error"] = "Security Check Failed! Invalid Admin Password or Security Key.";
                return RedirectToAction(nameof(Vault));
            }

            adminUser.TokenBalance += amount;
            await _userManager.UpdateAsync(adminUser);

            var vaultTransaction = new TokenTransaction
            {
                SenderUserId = adminUser.Id,
                SenderName = "System Treasury",
                ReceiverUserId = adminUser.Id,
                ReceiverName = "Admin Central Vault",
                Amount = amount,
                Type = TransactionType.VaultDeposit,
                Description = $"Vault Injection: +{amount} Texas Tokens deposited by {adminUser.FullName ?? adminUser.Email}.",
                Timestamp = DateTime.Now
            };

            _context.TokenTransactions.Add(vaultTransaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully minted {amount} Tokens into Admin Vault! New Reserve: {adminUser.TokenBalance} Tokens.";
            return RedirectToAction(nameof(Vault));
        }

        // 15. GET: /Admin/Statements
        [HttpGet]
        public async Task<IActionResult> Statements(string? viewType, int? selectedMonth, int? selectedYear)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            int currentMonth = selectedMonth ?? today.Month;
            int currentYear = selectedYear ?? today.Year;

            ViewBag.SelectedMonth = currentMonth;
            ViewBag.SelectedYear = currentYear;
            ViewBag.ViewType = viewType ?? "today";

            ViewBag.TodaysVolume = await _context.TokenTransactions
                .Where(t => t.Timestamp >= today && t.Timestamp < tomorrow && t.Type == TransactionType.Purchase)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            ViewBag.TodaysTxCount = await _context.TokenTransactions
                .Where(t => t.Timestamp >= today && t.Timestamp < tomorrow)
                .CountAsync();

            var allUsers = await _userManager.Users.ToListAsync();
            decimal circulatingTokens = 0m;
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Admin"))
                {
                    circulatingTokens += user.TokenBalance;
                }
            }
            ViewBag.TotalCirculatingVolume = circulatingTokens;

            IQueryable<TokenTransaction> query = _context.TokenTransactions.AsQueryable();

            if (viewType == "month")
            {
                query = query.Where(t => t.Timestamp.Month == currentMonth && t.Timestamp.Year == currentYear);
            }
            else if (viewType == "all")
            {
                // No date filter
            }
            else
            {
                query = query.Where(t => t.Timestamp >= today && t.Timestamp < tomorrow);
            }

            var transactions = await query.OrderByDescending(t => t.Timestamp).ToListAsync();
            return View(transactions);
        }

        // 16. GET: /Admin/ExportTransactionsCsv
        [HttpGet]
        public async Task<IActionResult> ExportTransactionsCsv(string? viewType, int? selectedMonth, int? selectedYear)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            int currentMonth = selectedMonth ?? today.Month;
            int currentYear = selectedYear ?? today.Year;

            IQueryable<TokenTransaction> query = _context.TokenTransactions.AsQueryable();

            if (viewType == "month")
            {
                query = query.Where(t => t.Timestamp.Month == currentMonth && t.Timestamp.Year == currentYear);
            }
            else if (viewType == "today")
            {
                query = query.Where(t => t.Timestamp >= today && t.Timestamp < tomorrow);
            }

            var list = await query.OrderByDescending(t => t.Timestamp).ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Transaction ID,Timestamp,Sender,Recipient,Type,Amount,Description");

            foreach (var tx in list)
            {
                string safeDescription = (tx.Description ?? string.Empty).Replace("\"", "\"\"");
                builder.AppendLine($"#TX-{tx.Id:D5},\"{tx.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{tx.SenderName}\",\"{tx.ReceiverName}\",\"{tx.Type}\",{tx.Amount},\"{safeDescription}\"");
            }

            string fileName = $"Token_Statement_{viewType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", fileName);
        }

        // 17. POST: /Admin/TopUpTokens
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUpTokens(string userId, decimal amount)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Top-up amount must be greater than 0.";
                return RedirectToAction(nameof(Vault));
            }

            var adminUser = await _userManager.GetUserAsync(User);
            if (adminUser == null || adminUser.TokenBalance < amount)
            {
                TempData["Error"] = $"Insufficient tokens in Admin Vault! Current Vault Balance: {adminUser?.TokenBalance ?? 0} Tokens.";
                return RedirectToAction(nameof(Vault));
            }

            var recipientUser = await _userManager.FindByIdAsync(userId);
            if (recipientUser == null)
            {
                TempData["Error"] = "Recipient user not found.";
                return RedirectToAction(nameof(Vault));
            }

            adminUser.TokenBalance -= amount;
            recipientUser.TokenBalance += amount;

            await _userManager.UpdateAsync(adminUser);
            await _userManager.UpdateAsync(recipientUser);

            var transaction = new TokenTransaction
            {
                SenderUserId = adminUser.Id,
                SenderName = "Admin Vault",
                ReceiverUserId = recipientUser.Id,
                ReceiverName = recipientUser.FullName ?? recipientUser.Email ?? "User",
                Amount = amount,
                Type = TransactionType.TopUp,
                Description = $"Transfer: {amount} Texas Tokens credited to {recipientUser.FullName ?? recipientUser.Email}.",
                Timestamp = DateTime.Now
            };

            _context.TokenTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Transferred {amount} Tokens to {recipientUser.FullName ?? recipientUser.Email}. Remaining Vault Reserve: {adminUser.TokenBalance} Tokens.";
            return RedirectToAction(nameof(Vault));
        }

        // 18. GET: /Admin/Transactions
        [HttpGet]
        public async Task<IActionResult> Transactions()
        {
            var transactions = await _context.TokenTransactions
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();

            return View(transactions);
        }

        // 19. GET: /Admin/Users
        [HttpGet]
        public async Task<IActionResult> Users(string? roleFilter)
        {
            var adminUser = await _userManager.GetUserAsync(User);
            ViewBag.AdminTokenBalance = adminUser?.TokenBalance ?? 0m;
            ViewBag.CurrentFilter = roleFilter;

            var userList = await _userManager.Users.ToListAsync();
            var userViewModels = new List<UserWithRoleViewModel>();

            foreach (var user in userList)
            {
                var roles = await _userManager.GetRolesAsync(user);
                string primaryRole = roles.FirstOrDefault() ?? "Student";

                if (!string.IsNullOrEmpty(roleFilter) && !primaryRole.Equals(roleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                userViewModels.Add(new UserWithRoleViewModel
                {
                    User = user,
                    Role = primaryRole
                });
            }

            return View(userViewModels);
        }
    }
}