using FeedMe.Models;
using System.Collections.Generic;

namespace FeedMe.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public List<Order> PendingOrders { get; set; } = new List<Order>();
        public int PendingOrdersCount { get; set; }
        public int TodaysOrdersCount { get; set; }
        public decimal TodaysRevenue { get; set; }
        public int LowStockItemsCount { get; set; }

        // Array holding order counts for Sunday (0) through Saturday (6)
        public int[] WeeklyOrderCounts { get; set; } = new int[7];
    }
}