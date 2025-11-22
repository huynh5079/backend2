using DataLayer.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface INotificationService
    {
        Task<Notification> CreateWalletNotificationAsync(string userId, DataLayer.Enum.NotificationType type, decimal amount, string? note = null, string? relatedEntityId = null, CancellationToken ct = default);
        Task<Notification> CreateEscrowNotificationAsync(string userId, DataLayer.Enum.NotificationType type, decimal amount, string classId, string? escrowId = null, CancellationToken ct = default);
        Task<Notification> CreateAccountNotificationAsync(string userId, DataLayer.Enum.NotificationType type, string? reason = null, string? relatedEntityId = null, CancellationToken ct = default);
        Task<Notification> CreateSystemAnnouncementNotificationAsync(string userId, string title, string message, string? relatedEntityId = null, CancellationToken ct = default);
        Task SendRealTimeNotificationAsync(string userId, Notification notification, CancellationToken ct = default);
    }
}

