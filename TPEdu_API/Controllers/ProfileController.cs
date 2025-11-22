using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Profile;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _svc;

        public ProfileController(IProfileService svc) => _svc = svc;

        [HttpGet("student")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStudent()
        {
            var uid = User.RequireUserId();
            var dto = await _svc.GetStudentProfileAsync(uid);
            return Ok(ApiResponse<object>.Ok(dto, "xem hồ sơ học sinh thành công"));
        }

        [HttpGet("parent")]
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> GetParent()
        {
            var uid = User.RequireUserId();
            var dto = await _svc.GetParentProfileAsync(uid);
            return Ok(ApiResponse<object>.Ok(dto, "xem hồ sơ phụ huynh thành công"));
        }

        [HttpGet("tutor")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetTutor()
        {
            var uid = User.RequireUserId();
            var dto = await _svc.GetTutorProfileAsync(uid);
            return Ok(ApiResponse<object>.Ok(dto, "xem hồ sơ gia sư thành công"));
        }

        [HttpPut("update/student")]
        public async Task<IActionResult> UpdateStudent([FromBody] UpdateStudentProfileRequest req)
        {
            if (!ModelState.IsValid)
                throw new ArgumentException("dữ liệu cập nhật không hợp lệ");

            var uid = User.RequireUserId();
            await _svc.UpdateStudentAsync(uid, req);
            return Ok(ApiResponse<object>.Ok(new { }, "cập nhật thành công"));
        }

        [HttpPut("update/parent")]
        public async Task<IActionResult> UpdateParent([FromBody] UpdateParentProfileRequest req)
        {
            if (!ModelState.IsValid)
                throw new ArgumentException("dữ liệu cập nhật không hợp lệ");

            var uid = User.RequireUserId();
            await _svc.UpdateParentAsync(uid, req);
            return Ok(ApiResponse<object>.Ok(new { }, "cập nhật thành công"));
        }

        [HttpPut("update/tutor")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> UpdateTutor([FromForm] UpdateTutorProfileRequest req, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                throw new ArgumentException("dữ liệu cập nhật không hợp lệ");

            var uid = User.RequireUserId();
            await _svc.UpdateTutorAsync(uid, req, ct);
            return Ok(ApiResponse<object>.Ok(new { }, "cập nhật thành công"));
        }
    }
}
