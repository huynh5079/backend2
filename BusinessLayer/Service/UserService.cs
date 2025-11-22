using BusinessLayer.Service.Interface;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notificationService;

        public UserService(IUnitOfWork uow, INotificationService notificationService)
        {
            _uow = uow;
            _notificationService = notificationService;
        }

        public async Task<(bool ok, string message)> BanAsync(string targetUserId, string adminId, string? reason, int? durationDays)
        {
            var user = await _uow.Users.GetByIdAsync(targetUserId);
            if (user == null) 
                throw new KeyNotFoundException("không tìm thấy tài khoản");
            if (user.RoleName == "Admin") 
                throw new InvalidOperationException("không thể khóa tài khoản Admin");

            if (user.IsBanned || user.Status == AccountStatus.Banned)
                return (true, "đã khoá tài khoản");

            user.IsBanned = true;
            user.Status = AccountStatus.Banned;
            user.BannedAt = DateTime.Now;
            user.BannedReason = reason;
            user.BannedUntil = durationDays.HasValue ? DateTime.Now.AddDays(durationDays.Value) : null;
            user.UpdatedAt = DateTime.Now;

            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            var notification = await _notificationService.CreateAccountNotificationAsync(
                user.Id,
                NotificationType.AccountBlocked,
                reason,
                relatedEntityId: user.Id);
            await _uow.SaveChangesAsync();
            await _notificationService.SendRealTimeNotificationAsync(user.Id, notification);

            return (true, "đã khoá tài khoản");
        }

        public async Task<(bool ok, string message)> UnbanAsync(string targetUserId, string adminId, string? reason)
        {
            var user = await _uow.Users.GetByIdAsync(targetUserId);
            if (user == null) 
                throw new KeyNotFoundException("không tìm thấy tài khoản");

            if (!user.IsBanned && user.Status != AccountStatus.Banned)
                return (true, "đã mở khoá tài khoản");

            user.IsBanned = false;
            user.Status = AccountStatus.Active;
            user.BannedUntil = null;
            user.BannedReason = null;
            user.UpdatedAt = DateTime.Now;

            await _uow.Users.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            var notification = await _notificationService.CreateAccountNotificationAsync(
                user.Id,
                NotificationType.AccountVerified,
                reason,
                relatedEntityId: user.Id);
            await _uow.SaveChangesAsync();
            await _notificationService.SendRealTimeNotificationAsync(user.Id, notification);

            return (true, "đã mở khoá tài khoản");
        }

        public async Task<IReadOnlyList<object>> GetBannedUsersAsync()
        {
            var rs = await _uow.Users.GetAllAsync(u => u.IsBanned || u.Status == AccountStatus.Banned);
            return rs.Select(u => new
            {
                u.Id,
                u.Email,
                u.UserName,
                u.Status,
                u.BannedAt,
                u.BannedUntil,
                u.BannedReason
            }).ToList();
        }
    }
}
