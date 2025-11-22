using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TPEdu_API.Common.Extensions;
using System.Threading.Tasks;

namespace TPEdu_API.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        // Khi client connect, map connection với UserId
        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.User?.RequireUserId();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    // Thêm connection vào group theo UserId
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                }
            }
            catch
            {
                // Nếu không có user (unauthorized), đóng connection
                Context.Abort();
            }
            await base.OnConnectedAsync();
        }

        // Khi client disconnect, remove khỏi group
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.GetUserId();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}

