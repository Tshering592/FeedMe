using System;
using Microsoft.AspNetCore.Identity;

namespace FeedMe.Models
{
    public class AppUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? StudentId { get; set; }
        public string? StaffId { get; set; }
        public string? ContactNumber { get; set; }
        public string? DietaryPreference { get; set; }
        public string? StudentLcid { get; set; }
        public string? Semester { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public decimal TokenBalance { get; set; } = 0m;
    }
}