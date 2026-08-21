using FeedMe.Models;


namespace FeedMe.Services
{
    public interface ITimeSlotService
    {
        Task<List<TimeSlot>> GetActiveSlotsAsync();
        Task<TimeSlot> GetSlotByIdAsync(int id);
        Task<bool> IsSlotAvailableAsync(int slotId, int orderCount);
        Task AddTimeSlotAsync(TimeSlot slot);
        Task UpdateTimeSlotAsync(TimeSlot slot);
        Task ToggleSlotActiveAsync(int id);
    }
}
