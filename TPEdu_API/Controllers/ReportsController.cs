using BusinessLayer.DTOs.API;
using BusinessLayer.Helper;
using BusinessLayer.Reports;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _svc;
        public ReportsController(IReportService svc) => _svc = svc;

        // Student tạo report tới Tutor
        [HttpPost("lessons/{lessonId}/materials/{mediaId}/report")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CreateToTutor(string lessonId, string mediaId, [FromBody] ReportCreateRequest req)
        {
            var uid = User.RequireUserId();
            var id = await _svc.CreateToTutorAsync(uid, lessonId, mediaId, req.Reason);
            return CreatedAtAction(nameof(GetDetailForTutor), new { id }, new { id });
        }

        // Student tạo report tới Admin
        [HttpPost("lessons/{lessonId}/materials/{mediaId}/report-to-admin")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CreateToAdmin(string lessonId, string mediaId, [FromBody] ReportCreateRequest req)
        {
            var uid = User.RequireUserId();
            var id = await _svc.CreateToAdminAsync(uid, lessonId, mediaId, req.Reason);
            return CreatedAtAction(nameof(GetDetailForAdmin), new { id }, new { id });
        }

        // Tutor – list report gửi cho mình
        [HttpGet("tutor")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetForTutor([FromQuery] ReportQuery q)
        {
            var uid = User.RequireUserId();
            var (items, total) = await _svc.GetForTutorAsync(uid, q);
            return Ok(ApiResponse<object>.Ok(new { total, items }, "Lấy danh sách report (tutor) thành công"));
        }

        // Admin – list report gửi cho admin
        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetForAdmin([FromQuery] ReportQuery q)
        {
            var (items, total) = await _svc.GetForAdminAsync(q);
            return Ok(ApiResponse<object>.Ok(new { total, items }, "Lấy danh sách report (admin) thành công"));
        }

        // Tutor xem detail report của mình
        [HttpGet("tutor/{id}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetDetailForTutor(string id)
        {
            var uid = User.RequireUserId();
            var dto = await _svc.GetDetailAsync(uid, id, isAdmin: false);
            return Ok(ApiResponse<object>.Ok(dto, "Lấy chi tiết report thành công"));
        }

        // Admin xem detail
        [HttpGet("admin/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDetailForAdmin(string id)
        {
            var uid = User.RequireUserId(); // không dùng, nhưng giữ consistency
            var dto = await _svc.GetDetailAsync(uid, id, isAdmin: true);
            return Ok(ApiResponse<object>.Ok(dto, "Lấy chi tiết report thành công"));
        }

        // Tutor cập nhật trạng thái report của mình
        [HttpPatch("tutor/{id}/status")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> UpdateStatusForTutor(string id, [FromBody] ReportUpdateStatusRequest req)
        {
            var uid = User.RequireUserId();
            var ok = await _svc.UpdateStatusAsync(uid, id, req.Status, isAdmin: false);
            return ok ? Ok(ApiResponse<object>.Ok(new { }, "Cập nhật trạng thái thành công"))
                      : BadRequest(ApiResponse<object>.Fail("Cập nhật thất bại"));
        }

        // Admin cập nhật trạng thái report gửi admin
        [HttpPatch("admin/{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatusForAdmin(string id, [FromBody] ReportUpdateStatusRequest req)
        {
            var uid = User.RequireUserId();
            var ok = await _svc.UpdateStatusAsync(uid, id, req.Status, isAdmin: true);
            return ok ? Ok(ApiResponse<object>.Ok(new { }, "Cập nhật trạng thái thành công"))
                      : BadRequest(ApiResponse<object>.Fail("Cập nhật thất bại"));
        }
    }
}
