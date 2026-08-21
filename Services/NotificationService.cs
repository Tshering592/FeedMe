using FeedMe.Data;
using FeedMe.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeedMe.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, UserManager<AppUser> userManager, ILogger<NotificationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // Send a notification to a specific user
        // IMPORTANT: this must never throw. A notification failing to save should never
        // take down the calling action (e.g. an order cancellation) after the "real"
        // work (refund, stock restore, status change) has already been committed.
        public async Task CreateNotificationAsync(string userId, string message, string type, int? orderId = null, string? title = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Skipped creating notification (Type={Type}, OrderId={OrderId}) because userId was empty.", type, orderId);
                return;
            }

            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Title = title ?? string.Empty,
                    Message = message,
                    Type = type,
                    OrderId = orderId,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Swallow and log rather than letting a notification failure bubble up
                // and abort the caller's action (this was the root cause of kitchen-side
                // cancellations refunding tokens but never notifying the student).
                _logger.LogError(ex, "Failed to create notification for user {UserId}, order {OrderId}.", userId, orderId);
            }
        }

        // Send a notification to all users in both "Staff" and "Admin" roles
        public async Task NotifyKitchenAsync(string message, int? orderId = null)
        {
            var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

            // Merge staff and admin users without duplicates
            var kitchenUsers = staffUsers.Concat(adminUsers).DistinctBy(u => u.Id).ToList();

            if (!kitchenUsers.Any()) return;

            var notifications = kitchenUsers.Select(user => new Notification
            {
                UserId = user.Id,
                Message = message,
                Type = "OrderUpdate",
                OrderId = orderId,
                IsRead = false,
                CreatedAt = DateTime.Now
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        // Send a notification to all users in a specific role (e.g., "Staff", "Admin")
        public async Task NotifyRoleAsync(string roleName, string message, string type, int? orderId = null)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            if (!usersInRole.Any()) return;

            var notifications = usersInRole.Select(user => new Notification
            {
                UserId = user.Id,
                Message = message,
                Type = type,
                OrderId = orderId,
                IsRead = false,
                CreatedAt = DateTime.Now
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        // Fetch unread notifications for header dropdown
        public async Task<List<Notification>> GetUnreadNotificationsAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        // Fetch all notifications for the dedicated Notifications page
        public async Task<List<Notification>> GetAllNotificationsAsync(string userId, int count = 50)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        // Mark a single notification as read
        public async Task MarkAsReadAsync(int notificationId)
        {
            var notif = await _context.Notifications.FindAsync(notificationId);
            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        // Mark all notifications as read for a specific user
        public async Task MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unread.Any())
            {
                foreach (var notif in unread)
                {
                    notif.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
        }

        // Get count of unread notifications
        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }
    }
}