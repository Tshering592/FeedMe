using System;
using System.Collections.Generic;

namespace FeedMe.Models
{
    // Complete enum matching all status states in your system
    public enum OrderStatus
    {
        Pending,
        Accepted,
        Preparing,
        Ready,
        Collected,
        Completed,
        Rejected,
        Cancelled
    }

    public class Order
    {
        public int Id { get; set; }

        // Link to AppUser model (ASP.NET Identity / User System)
        public string? StudentId { get; set; }
        public virtual AppUser? Student { get; set; }

        // Mobile App Metadata Fields
        public string StudentLcid { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;

        // Staff Notes & Admin Notification Messages
        public string? AdminMessage { get; set; }
        public string? StaffNotes { get; set; }

        // Time Slot Relationship
        public int? TimeSlotId { get; set; }
        public virtual TimeSlot? TimeSlot { get; set; }

        // Order Timestamps
        public DateTime OrderTime { get; set; } = DateTime.Now;
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? EstimatedReadyTime { get; set; }
        public DateTime? ActualReadyTime { get; set; }
        public DateTime? CollectionTime { get; set; }

        // Status and Totals
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public decimal TotalAmount { get; set; }

        // Order Items Navigation Property
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}