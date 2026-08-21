using FeedMe.Data;
using FeedMe.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedMe.Services
{
    public class TimeSlotService : ITimeSlotService
    {
        private readonly ApplicationDbContext _context;

        public TimeSlotService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TimeSlot>> GetActiveSlotsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.TimeSlots
                .Where(s => s.IsActive && s.EndTime > now)
                .OrderBy(s => s.StartTime)
                .ToListAsync();
        }

        public async Task<TimeSlot> GetSlotByIdAsync(int id)
        {
            return await _context.TimeSlots
                .Include(s => s.Orders)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<bool> IsSlotAvailableAsync(int slotId, int orderCount)
        {
            var slot = await GetSlotByIdAsync(slotId);
            if (slot == null || !slot.IsActive) return false;
            var currentOrders = slot.Orders?.Count(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Collected) ?? 0;
            return (currentOrders + orderCount) <= slot.MaxOrders;
        }

        public async Task AddTimeSlotAsync(TimeSlot slot)
        {
            _context.TimeSlots.Add(slot);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateTimeSlotAsync(TimeSlot slot)
        {
            _context.TimeSlots.Update(slot);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleSlotActiveAsync(int id)
        {
            var slot = await GetSlotByIdAsync(id);
            if (slot != null)
            {
                slot.IsActive = !slot.IsActive;
                await _context.SaveChangesAsync();
            }
        }
    }
}
