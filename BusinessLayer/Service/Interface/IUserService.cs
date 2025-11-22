using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IUserService
    {
        Task<(bool ok, string message)> BanAsync(string targetUserId, string adminId, string? reason, int? durationDays);
        Task<(bool ok, string message)> UnbanAsync(string targetUserId, string adminId, string? reason);
        Task<IReadOnlyList<object>> GetBannedUsersAsync(); // optional: list
    }
}
