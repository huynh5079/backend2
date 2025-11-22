using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;

namespace BusinessLayer.Service
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationHubService _hubService;

        public NotificationService(IUnitOfWork uow, INotificationHubService hubService)
        {
            _uow = uow;
            _hubService = hubService;
        }

        public async Task<Notification> CreateWalletNotificationAsync(string userId, NotificationType type, decimal amount, string? note = null, string? relatedEntityId = null, CancellationToken ct = default)
        {
            var (title, message) = GetWalletNotificationContent(type, amount, note);
            
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Status = NotificationStatus.Unread,
                Title = title,
                Message = message,
                RelatedEntityId = relatedEntityId
            };

            await _uow.Notifications.AddAsync(notification, ct);
            return notification;
        }
        
        public async Task SendRealTimeNotificationAsync(string userId, Notification notification, CancellationToken ct = default)
        {
            var dto = new DTOs.Notification.NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type.ToString(),
                Status = notification.Status.ToString(),
                RelatedEntityId = notification.RelatedEntityId,
                CreatedAt = notification.CreatedAt,
                UpdatedAt = notification.UpdatedAt
            };
            
            await _hubService.SendNotificationToUserAsync(userId, dto);
        }

        public async Task<Notification> CreateEscrowNotificationAsync(string userId, NotificationType type, decimal amount, string classId, string? escrowId = null, CancellationToken ct = default)
        {
            var (title, message) = GetEscrowNotificationContent(type, amount, classId);
            
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Status = NotificationStatus.Unread,
                Title = title,
                Message = message,
                RelatedEntityId = escrowId ?? classId
            };

            await _uow.Notifications.AddAsync(notification, ct);
            return notification;
        }

        public async Task<Notification> CreateAccountNotificationAsync(
            string userId,
            NotificationType type,
            string? reason = null,
            string? relatedEntityId = null,
            CancellationToken ct = default)
        {
            var (title, message) = GetAccountNotificationContent(type, reason);

            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Status = NotificationStatus.Unread,
                Title = title,
                Message = message,
                RelatedEntityId = relatedEntityId
            };

            await _uow.Notifications.AddAsync(notification, ct);
            return notification;
        }

        public async Task<Notification> CreateSystemAnnouncementNotificationAsync(
            string userId,
            string title,
            string message,
            string? relatedEntityId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "Thông báo hệ thống";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Hệ thống có thông báo mới.";
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.SystemAnnouncement,
                Status = NotificationStatus.Unread,
                Title = title,
                Message = message,
                RelatedEntityId = relatedEntityId
            };

            await _uow.Notifications.AddAsync(notification, ct);
            return notification;
        }

        private (string title, string message) GetWalletNotificationContent(NotificationType type, decimal amount, string? note)
        {
            var formattedAmount = amount.ToString("N0", new CultureInfo("vi-VN")) + " VND";
            
            return type switch
            {
                NotificationType.WalletDeposit => (
                    "Nạp tiền thành công",
                    $"Bạn đã nạp {formattedAmount} vào ví thành công.{(string.IsNullOrWhiteSpace(note) ? "" : $" Ghi chú: {note}")}"
                ),
                NotificationType.WalletWithdraw => (
                    "Rút tiền thành công",
                    $"Bạn đã rút {formattedAmount} từ ví thành công.{(string.IsNullOrWhiteSpace(note) ? "" : $" Ghi chú: {note}")}"
                ),
                NotificationType.WalletTransferIn => (
                    "Nhận chuyển tiền",
                    $"Bạn đã nhận {formattedAmount} từ chuyển khoản.{(string.IsNullOrWhiteSpace(note) ? "" : $" Ghi chú: {note}")}"
                ),
                NotificationType.WalletTransferOut => (
                    "Chuyển tiền thành công",
                    $"Bạn đã chuyển {formattedAmount} thành công.{(string.IsNullOrWhiteSpace(note) ? "" : $" Ghi chú: {note}")}"
                ),
                _ => ("Thông báo ví", $"Giao dịch {formattedAmount} đã được thực hiện.")
            };
        }

        private (string title, string message) GetEscrowNotificationContent(NotificationType type, decimal amount, string classId)
        {
            var formattedAmount = amount.ToString("N0", new CultureInfo("vi-VN")) + " VND";
            
            return type switch
            {
                NotificationType.EscrowPaid => (
                    "Thanh toán TPEdu thành công",
                    $"Bạn đã thanh toán {formattedAmount} vào TPEdu cho lớp học. Tiền sẽ được giữ cho đến khi lớp học hoàn thành."
                ),
                NotificationType.EscrowReleased => (
                    "Nhận thanh toán từ TPEdu",
                    $"Bạn đã nhận {formattedAmount} từ TPEdu. Lớp học đã hoàn thành và tiền đã được giải ngân."
                ),
                NotificationType.EscrowRefunded => (
                    "Hoàn tiền TPEdu",
                    $"Bạn đã nhận lại {formattedAmount} từ TPEdu. Tiền đã được hoàn về ví của bạn."
                ),
                NotificationType.PayoutReceived => (
                    "Nhận thanh toán",
                    $"Bạn đã nhận {formattedAmount} từ lớp học. Tiền đã được chuyển vào ví của bạn."
                ),
                _ => ("Thông báo TPEdu", $"Giao dịch TPEdu {formattedAmount} đã được thực hiện.")
            };
        }

        private (string title, string message) GetAccountNotificationContent(NotificationType type, string? reason)
        {
            return type switch
            {
                NotificationType.AccountVerified => (
                    "Tài khoản đã được xác minh",
                    "Tài khoản của bạn đã được xác minh thành công."
                ),
                NotificationType.AccountBlocked => (
                    "Tài khoản bị tạm khóa",
                    $"Tài khoản của bạn đã bị tạm khóa.{(string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason}")}"
                ),
                NotificationType.TutorApproved => (
                    "Hồ sơ gia sư được duyệt",
                    "Hồ sơ của bạn đã được duyệt. Bạn có thể bắt đầu nhận lớp."
                ),
                NotificationType.TutorRejected => (
                    "Hồ sơ gia sư bị từ chối",
                    $"Hồ sơ của bạn bị từ chối.{(string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason}")}"
                ),
                NotificationType.SystemAnnouncement => (
                    "Thông báo hệ thống",
                    "Hệ thống có thông báo mới."
                ),
                _ => ("Thông báo tài khoản", "Tài khoản của bạn vừa được cập nhật trạng thái.")
            };
        }
    }
}

