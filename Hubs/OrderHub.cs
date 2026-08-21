using Microsoft.AspNetCore.SignalR;

namespace FeedMe.Hubs
{
    public class OrderHub : Hub
    {
        public async Task NotifyOrderStatusChange(string userId, string message)
        {
            await Clients.User(userId).SendAsync("ReceiveOrderNotification", message);
        }

        // Optionally broadcast to staff group
        public async Task NotifyStaff(string message)
        {
            await Clients.Group("Staff").SendAsync("ReceiveStaffNotification", message);
        }

        public override async Task OnConnectedAsync()
        {
            // Add to role-based group if authenticated
            if (Context.User?.IsInRole("Staff") == true)
                await Groups.AddToGroupAsync(Context.ConnectionId, "Staff");
            await base.OnConnectedAsync();
        }
    }
}
