using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TPEdu_API.Common.Extensions;
using TPEdu_API.Services;
using System.Threading.Tasks;

namespace TPEdu_API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatConnectionManager _connectionManager;

        public ChatHub(ChatConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

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
                    
                    // Track connection
                    bool isNewlyOnline = !_connectionManager.IsUserOnline(userId);
                    _connectionManager.AddConnection(userId, Context.ConnectionId);
                    
                    // Nếu user mới online (không có connection nào trước đó), broadcast
                    if (isNewlyOnline)
                    {
                        // Broadcast cho tất cả clients rằng user này đã online
                        await Clients.All.SendAsync("UserOnline", userId);
                    }
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
                
                // Remove connection và kiểm tra xem user có còn online không
                bool isNowOffline = _connectionManager.RemoveConnection(Context.ConnectionId);
                
                // Nếu user không còn connection nào, broadcast offline
                if (isNowOffline)
                {
                    await Clients.All.SendAsync("UserOffline", userId);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}

