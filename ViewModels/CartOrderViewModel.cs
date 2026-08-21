using System.Collections.Generic;

namespace FeedMe.ViewModels
{
    public class CartItemSelection
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class CartOrderViewModel
    {
        public int TimeSlotId { get; set; }
        public List<CartItemSelection> SelectedItems { get; set; } = new List<CartItemSelection>();
    }
}