using BusinessLayer.DTOs.Admin.Users;
using BusinessLayer.DTOs.API;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _svc;
        public UsersController(IUserService svc) => _svc = svc;

        [HttpGet("banned")]
        public async Task<IActionResult> GetBannedUsers()
        {
            var data = await _svc.GetBannedUsersAsync();
            return Ok(ApiResponse<object>.Ok(data, "lấy danh sách tài khoản bị khóa thành công"));
        }

        [HttpPut("ban_account/{userId}")]
        public async Task<IActionResult> BanUser(string userId, [FromBody] BanUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId không hợp lệ");

            var adminId = User.RequireUserId();
            var (ok, msg) = await _svc.BanAsync(userId, adminId, req?.Reason, req?.DurationDays);
            if (!ok) throw new InvalidOperationException(msg);

            return Ok(ApiResponse<object>.Ok(new { }, msg));
        }

        [HttpPut("unban_account/{userId}")]
        public async Task<IActionResult> UnbanUser(string userId, [FromBody] UnbanUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId không hợp lệ");

            var adminId = User.RequireUserId();
            var (ok, msg) = await _svc.UnbanAsync(userId, adminId, req?.Reason);
            if (!ok) throw new InvalidOperationException(msg);

            return Ok(ApiResponse<object>.Ok(new { }, msg));
        }
    }
}
