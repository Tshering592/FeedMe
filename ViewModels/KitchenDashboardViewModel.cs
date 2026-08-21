using FeedMe.Models;
using System.Collections.Generic;

namespace FeedMe.ViewModels
{
    public class KitchenDashboardViewModel
    {
        public int TotalOrdersToday { get; set; }
        public int PendingOrders { get; set; }
        public int PreparingOrders { get; set; }
        public int ReadyOrders { get; set; }
        public decimal TotalRevenueToday { get; set; }

        public List<Order> ActiveOrders { get; set; } = new();
    }
}