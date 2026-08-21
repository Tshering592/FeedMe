using FeedMe.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeedMe.Services
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(string studentId, int slotId, List<OrderItemDto> items);
        Task<List<Order>> GetOrdersForStudentAsync(string studentId);
        Task<List<Order>> GetOrdersForSlotAsync(int slotId);
        Task<List<Order>> GetOrdersForStaffAsync(OrderStatus? status = null);
        Task<List<Order>> GetAllOrdersAsync();
        Task<Order?> GetOrderByIdAsync(int id);
        Task<bool> AcceptOrderAsync(int orderId, DateTime estimatedReadyTime);
        Task<bool> RejectOrderAsync(int orderId, string reason);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);
        Task<bool> CancelOrderAsync(int orderId, string studentId);
        Task<bool> AdminCancelOrderAsync(int orderId, string reason, string cancelledBy = "Admin");
        Task<bool> MarkCollectedAsync(int orderId);
    }
}