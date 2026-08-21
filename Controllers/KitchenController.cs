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
using System.Threading.Tasks;

namespace FeedMe.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class KitchenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly IOrderService _orderService;

        public KitchenController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            INotificationService notificationService,
            IOrderService orderService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _orderService = orderService;
        }

        // GET: /Kitchen/Dashboard?filter=all
        [HttpGet]
        public async Task<IActionResult> Dashboard(string filter = "all")
        {
            var userId = _userManager.GetUserId(User);
            var today = DateTime.Today;  

            var ordersToday = await _context.Orders
                .Where(o => o.OrderTime.Date == today)
                .ToListAsync();

            // Base query for active orders list
            var baseQuery = _context.Orders
                .Where(o => o.OrderTime.Date == today)
                .Include(o => o.TimeSlot)
                .Include(o => o.Student)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .AsQueryable();

            // Filter logic for table view
            var normalizedFilter = filter.ToLower();
            IQueryable<Order> filteredOrders = normalizedFilter switch
            {
                "preparing" => baseQuery.Where(o => o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Pending || o.Status == OrderStatus.Accepted),
                "completed" => baseQuery.Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Ready || o.Status == OrderStatus.Collected),
                "cancelled" => baseQuery.Where(o => o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.Rejected),
                // Default (Live Queue): Excludes Completed, Ready, Collected, Cancelled, and Rejected orders
                _ => baseQuery.Where(o => o.Status != OrderStatus.Completed
                                       && o.Status != OrderStatus.Ready
                                       && o.Status != OrderStatus.Collected
                                       && o.Status != OrderStatus.Cancelled
                                       && o.Status != OrderStatus.Rejected)
            };

            ViewBag.CurrentFilter = normalizedFilter;

            // Fetch unread notification count via NotificationService for initial header rendering
            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.UnreadCount = await _notificationService.GetUnreadCountAsync(userId);
            }

            var viewModel = new KitchenDashboardViewModel
            {
                TotalOrdersToday = ordersToday.Count,
                PendingOrders = ordersToday.Count(o => o.Status == OrderStatus.Pending),
                PreparingOrders = ordersToday.Count(o => o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Accepted),
                ReadyOrders = ordersToday.Count(o => o.Status == OrderStatus.Ready),
                TotalRevenueToday = ordersToday
                    .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Rejected)
                    .Sum(o => o.TotalAmount),
                ActiveOrders = await filteredOrders
                    .OrderByDescending(o => o.OrderTime)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        // POST: /Kitchen/UpdateOrderStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, OrderStatus status, string? reason = null, int estimatedMinutes = 20)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                TempData["Error"] = "Order not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            bool isSuccess;

            // Handle workflow routing through OrderService methods
            if (status == OrderStatus.Accepted)
            {
                var estReadyTime = DateTime.UtcNow.AddMinutes(estimatedMinutes);
                isSuccess = await _orderService.AcceptOrderAsync(orderId, estReadyTime);
            }
            else if (status == OrderStatus.Rejected)
            {
                string rejectReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by Kitchen Staff" : reason;
                isSuccess = await _orderService.RejectOrderAsync(orderId, rejectReason);
            }
            else if (status == OrderStatus.Cancelled)
            {
                string cancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by Kitchen Staff" : reason;
                isSuccess = await _orderService.AdminCancelOrderAsync(orderId, cancelReason);
            }
            else
            {
                isSuccess = await _orderService.UpdateOrderStatusAsync(orderId, status);
            }

            if (!isSuccess)
            {
                TempData["Error"] = $"Unable to update Order #{order.Id:D3}. Check current order status.";
                return RedirectToAction(nameof(Dashboard));
            }

            TempData["Success"] = $"Order #{order.Id:D3} updated to {status}.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Kitchen/Notifications
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            // Fetch all notifications for the dedicated page using service
            var notifications = await _notificationService.GetAllNotificationsAsync(userId);

            // Automatically mark all as read upon visiting the page
            await _notificationService.MarkAllAsReadAsync(userId);

            ViewBag.UnreadCount = 0;

            return View(notifications);
        }

        // GET: /Kitchen/GetUnreadNotifications (AJAX endpoint for Header Dropdown / Bell Badge)
        [HttpGet]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { unreadCount = 0, items = new List<object>() });
            }

            var unreadNotifications = await _notificationService.GetUnreadNotificationsAsync(userId);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            var items = unreadNotifications.Take(10).Select(n => new
            {
                n.Id,
                n.Message,
                n.OrderId,
                n.IsRead,
                CreatedAt = n.CreatedAt.ToString("o") // Output as ISO 8601 string for JavaScript parsing
            });

            return Json(new { unreadCount, items });
        }

        // POST: /Kitchen/MarkAllNotificationsAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(userId);

            return Json(new { success = true });
        }

        // POST: /Kitchen/SavePushSubscription (Handles Web Push registration tokens)
        [HttpPost]
        public async Task<IActionResult> SavePushSubscription([FromBody] object subscription)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Endpoint ready to save device push token if storing push endpoints in DB
            return Json(new { success = true });
        }

        // GET: /Kitchen/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(user);
        }

        // POST: /Kitchen/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string contactNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(contactNumber))
            {
                TempData["Error"] = "Full name and phone number are required.";
                return RedirectToAction(nameof(Profile));
            }

            user.FullName = fullName.Trim();
            user.ContactNumber = contactNumber.Trim();
            user.PhoneNumber = contactNumber.Trim();

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Profile details updated successfully!";

            return RedirectToAction(nameof(Profile));
        }
    }
}