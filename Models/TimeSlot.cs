using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedMe.Models
{
    public class TimeSlot
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int MaxOrders { get; set; } = 20;
        public bool IsActive { get; set; } = true;

        // Fix: Added SlotName to match what your views/controllers are referencing
        [NotMapped]
        public string SlotName => $"{StartTime:hh:mm tt} - {EndTime:hh:mm tt}";

        // Optional alias if you also want to use SlotTime elsewhere
        [NotMapped]
        public string SlotTime => SlotName;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}