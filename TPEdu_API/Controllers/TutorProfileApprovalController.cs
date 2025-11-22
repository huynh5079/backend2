using BusinessLayer.DTOs.Admin.TutorProfileApproval;
using BusinessLayer.DTOs.API;
using BusinessLayer.Helper;
using BusinessLayer.Service;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;
using static BusinessLayer.DTOs.Admin.Tutors.TutorActionRequests;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/admin")]
    [Authorize(Roles = "Admin")]
    public class TutorProfileApprovalController : ControllerBase
    {
        private readonly ITutorProfileApprovalService _svcApproval;
        private readonly IAdminDirectoryService _svcDirectory;

        public TutorProfileApprovalController(ITutorProfileApprovalService svcApproval,
                                              IAdminDirectoryService svcDirectory)
        {
            _svcApproval = svcApproval;
            _svcDirectory = svcDirectory;
        }

        // GET tpedu/v1/admin/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var data = await _svcApproval.GetPendingAsync();
            return Ok(ApiResponse<object>.Ok(data, "lấy danh sách thành công"));
        }

        // GET tpedu/v1/admin/tutor?page=1 (5 items/page)
        [HttpGet("tutor")]
        public async Task<IActionResult> GetTutors([FromQuery] int page = 1)
        {
            var rs = await _svcDirectory.GetTutorsPagedAsync(page, 5);
            var data = rs.Data.Select(x => new
            {
                tutorId = x.TutorId,
                username = x.Username,
                email = x.Email,
                status = x.Status,
                isBanned = x.IsBanned,
                createDate = x.CreateDate
            });
            return Ok(ApiResponse<object>.Ok(data, "lấy danh sách thành công"));
        }

        // GET tpedu/v1/admin/tutor/detail/:id
        [HttpGet("tutor/detail/{id}")]
        public async Task<IActionResult> GetTutorDetail(string id)
        {
            var detail = await _svcApproval.GetDetailAsync(id);
            if (detail == null)
                return NotFound(ApiResponse<object>.Fail("bạn không có"));

            return Ok(ApiResponse<object>.Ok(detail, "lấy danh sách thành công"));
        }

        // PUT tpedu/v1/admin/tutor/accept/:id
        [HttpPut("tutor/accept/{id}")]
        public async Task<IActionResult> AcceptTutor(string id)
        {
            // var adminId = User.RequireUserId();
            var (ok, msg) = await _svcApproval.ApproveAsync(id);
            if (!ok) return NotFound(ApiResponse<object>.Fail("Gia sư không tồn tại"));

            var detail = await _svcApproval.GetDetailAsync(id);
            return Ok(ApiResponse<object>.Ok(detail, "Đã duyệt hồ sơ gia sư"));
        }

        // PUT tpedu/v1/admin/tutor/reject/:id
        [HttpPut("tutor/reject/{id}")]
        public async Task<IActionResult> RejectTutor(string id, [FromBody] RejectTutorRequest req)
        {
            // var adminId = User.RequireUserId();
            var (ok, msg) = await _svcApproval.RejectAsync(id, req?.RejectReason);
            if (!ok) return NotFound(ApiResponse<object>.Fail("Gia sư không tồn tại"));

            var detail = await _svcApproval.GetDetailAsync(id);
            return Ok(ApiResponse<object>.Ok(detail, "Đã từ chối hồ sơ gia sư"));
        }

        // PUT tpedu/v1/admin/tutor/provide/:id
        [HttpPut("tutor/provide/{id}")]
        public async Task<IActionResult> ProvideTutor(string id, [FromBody] ProvideTutorRequest req)
        {
            // var adminId = User.RequireUserId();

            var (ok, msg) = await _svcApproval.ProvideAsync(id, req?.ProvideText);
            if (!ok) return NotFound(ApiResponse<object>.Fail("Gia sư không tồn tại"));

            var detail = await _svcApproval.GetDetailAsync(id);
            return Ok(ApiResponse<object>.Ok(detail, "Đã yêu cầu bổ sung hồ sơ gia sư"));
        }
    }
}
