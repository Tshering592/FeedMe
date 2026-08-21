using FeedMe.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeedMe.Services
{
    public interface INotificationService
    {
        // Send a notification to a specific individual user.
        // Never throws - failures are logged internally so a notification issue
        // can never abort the caller's action (e.g. an order cancellation).
        Task CreateNotificationAsync(string userId, string message, string type, int? orderId = null, string? title = null);

        // Send a notification to all users in a specific role (e.g., "Staff", "Admin")
        Task NotifyRoleAsync(string roleName, string message, string type, int? orderId = null);

        // Broadcast notifications to all Kitchen Staff and Admins simultaneously
        Task NotifyKitchenAsync(string message, int? orderId = null);

        // Retrieve unread notifications for the bell dropdown
        Task<List<Notification>> GetUnreadNotificationsAsync(string userId);

        // Retrieve full notification history
        Task<List<Notification>> GetAllNotificationsAsync(string userId, int count = 50);

        // Mark individual or bulk notifications as read
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(string userId);

        // Count pending unread notifications for badge rendering
        Task<int> GetUnreadCountAsync(string userId);
    }
}