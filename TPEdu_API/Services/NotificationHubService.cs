using BusinessLayer.DTOs.Notification;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using TPEdu_API.Hubs;

namespace TPEdu_API.Services
{
    public class NotificationHubService : INotificationHubService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationHubService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendNotificationToUserAsync(string userId, NotificationDto notification)
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", notification);
        }
    }
}