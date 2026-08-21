using FeedMe.Data;
using FeedMe.DTOs;
using FeedMe.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeedMe.Services
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITimeSlotService _timeSlotService;
        private readonly INotificationService _notificationService;
        private readonly UserManager<AppUser> _userManager;

        public OrderService(ApplicationDbContext context,
                            ITimeSlotService timeSlotService,
                            INotificationService notificationService,
                            UserManager<AppUser> userManager)
        {
            _context = context;
            _timeSlotService = timeSlotService;
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<Order> CreateOrderAsync(string studentId, int slotId, List<OrderItemDto> items)
        {
            var totalItems = items.Sum(i => i.Quantity);
            if (!await _timeSlotService.IsSlotAvailableAsync(slotId, totalItems))
                throw new Exception("Time slot is full or unavailable.");

            var student = await _context.Users.FindAsync(studentId);
            if (student == null)
                throw new Exception("Student not found.");

            decimal total = 0;
            var orderItems = new List<OrderItem>();

            foreach (var dto in items)
            {
                var menuItem = await _context.MenuItems.FindAsync(dto.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                    throw new Exception($"Item ID {dto.MenuItemId} is not available.");

                if (menuItem.StockQuantity < dto.Quantity)
                    throw new Exception($"Insufficient stock for {menuItem.Name}. Available: {menuItem.StockQuantity}");

                menuItem.StockQuantity -= dto.Quantity;

                var orderItem = new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = dto.Quantity,
                    UnitPrice = menuItem.Price
                };

                orderItems.Add(orderItem);
                total += menuItem.Price * dto.Quantity;
            }

            // Deduct Tokens / Balance from Student
            if (student.TokenBalance < (int)total)
            {
                throw new Exception("Insufficient token balance to place this order.");
            }
            student.TokenBalance -= (int)total;

            var order = new Order
            {
                StudentId = studentId,
                TimeSlotId = slotId,
                TotalAmount = total,
                OrderItems = orderItems,
                Status = OrderStatus.Pending,
                OrderTime = DateTime.UtcNow
            };

            _context.Orders.Add(order);

            // Save order first so database generates primary key order.Id
            await _context.SaveChangesAsync();

            // Record Purchase Transaction log with valid Order ID
            var purchaseTransaction = new TokenTransaction
            {
                SenderUserId = studentId,
                SenderName = student.FullName ?? "Student",
                ReceiverUserId = "CANTEEN",
                ReceiverName = "TCMIT Canteen",
                Amount = total,
                Type = TransactionType.Purchase,
                Description = $"Meal Payment for Order #{order.Id:D3}",
                Timestamp = DateTime.UtcNow
            };
            _context.TokenTransactions.Add(purchaseTransaction);
            await _context.SaveChangesAsync();

            // 1. Notify the student who placed the order
            await _notificationService.CreateNotificationAsync(
                studentId,
                $"Your order #{order.Id:D3} has been placed successfully.",
                "OrderStatus",
                order.Id
            );

            // 2. Broadcast to all Kitchen / Staff / Admin members
            string notificationMsg = $"New Order #{order.Id:D3} received from {student.FullName ?? "Customer"}!";

            await _notificationService.NotifyRoleAsync("Admin", notificationMsg, "NewOrder", order.Id);
            await _notificationService.NotifyRoleAsync("Kitchen", notificationMsg, "NewOrder", order.Id);

            return order;
        }

        public async Task<List<Order>> GetOrdersForStudentAsync(string studentId)
        {
            return await _context.Orders
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.StudentId == studentId)
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();
        }

        public async Task<List<Order>> GetOrdersForSlotAsync(int slotId)
        {
            return await _context.Orders
                .Include(o => o.Student)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.TimeSlotId == slotId)
                .OrderBy(o => o.OrderTime)
                .ToListAsync();
        }

        public async Task<List<Order>> GetOrdersForStaffAsync(OrderStatus? status = null)
        {
            var query = _context.Orders
                .Include(o => o.Student)
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Collected);

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            return await query.OrderBy(o => o.OrderTime).ToListAsync();
        }

        public async Task<List<Order>> GetAllOrdersAsync()
        {
            return await _context.Orders
                .Include(o => o.Student)
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.Student)
                .Include(o => o.TimeSlot)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<bool> AcceptOrderAsync(int orderId, DateTime estimatedReadyTime)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null || order.Status != OrderStatus.Pending)
                return false;

            order.Status = OrderStatus.Accepted;
            order.EstimatedReadyTime = estimatedReadyTime;
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                order.StudentId,
                $"Your order #{order.Id:D3} has been accepted. Estimated ready at {estimatedReadyTime.ToLocalTime():hh:mm tt}.",
                "OrderStatus",
                order.Id
            );
            return true;
        }

        public async Task<bool> RejectOrderAsync(int orderId, string reason)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null || order.Status == OrderStatus.Rejected || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Collected)
                return false;

            RestoreOrderStock(order);
            RefundTokensToStudent(order, $"Refund for rejected Order #{order.Id:D3}");

            order.Status = OrderStatus.Rejected;
            order.StaffNotes = reason;
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                order.StudentId,
                $"Your order #{order.Id:D3} was rejected. Reason: {reason}. Tokens have been refunded.",
                "OrderStatus",
                order.Id
            );
            return true;
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null) return false;

            order.Status = newStatus;
            if (newStatus == OrderStatus.Ready)
                order.ActualReadyTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                order.StudentId,
                $"Your order #{order.Id:D3} is now {newStatus}.",
                "OrderStatus",
                order.Id
            );
            return true;
        }

        public async Task<bool> CancelOrderAsync(int orderId, string studentId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null || order.StudentId != studentId || order.Status != OrderStatus.Pending)
                return false;

            RestoreOrderStock(order);
            RefundTokensToStudent(order, $"Refund for cancelled Order #{order.Id:D3}");

            order.Status = OrderStatus.Cancelled;
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                studentId,
                $"Your order #{order.Id:D3} has been cancelled. Tokens have been refunded.",
                "OrderStatus",
                order.Id
            );
            return true;
        }

        public async Task<bool> AdminCancelOrderAsync(int orderId, string reason)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null || order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Rejected || order.Status == OrderStatus.Collected)
                return false;

            RestoreOrderStock(order);
            RefundTokensToStudent(order, $"Refund for cancelled Order #{order.Id:D3}");

            order.Status = OrderStatus.Cancelled;
            order.StaffNotes = $"Cancelled by Admin: {reason}";
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                order.StudentId,
                $"Your order #{order.Id:D3} was cancelled by Admin. Reason: {reason}. Tokens have been refunded.",
                "OrderStatus",
                order.Id
            );
            return true;
        }

        public async Task<bool> MarkCollectedAsync(int orderId)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null || order.Status != OrderStatus.Ready)
                return false;

            order.Status = OrderStatus.Collected;
            order.CollectionTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // Helper Methods
        private void RestoreOrderStock(Order order)
        {
            if (order?.OrderItems == null) return;

            foreach (var item in order.OrderItems)
            {
                if (item.MenuItem != null)
                {
                    item.MenuItem.StockQuantity += item.Quantity;
                }
            }
        }

        private void RefundTokensToStudent(Order order, string description)
        {
            if (order?.Student != null && order.TotalAmount > 0)
            {
                order.Student.TokenBalance += (int)order.TotalAmount;

                var transaction = new TokenTransaction
                {
                    SenderUserId = "SYSTEM",
                    SenderName = "TCMIT Canteen",
                    ReceiverUserId = order.StudentId,
                    ReceiverName = order.Student.FullName ?? "Student",
                    Amount = order.TotalAmount,
                    Type = TransactionType.Refund,
                    Description = description,
                    Timestamp = DateTime.UtcNow
                };

                _context.TokenTransactions.Add(transaction);
            }
        }
    }
}