using BusinessLayer.DTOs.Notification;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface INotificationHubService
    {
        Task SendNotificationToUserAsync(string userId, NotificationDto notification);
    }
}

