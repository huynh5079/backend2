using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.ClassRequest;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{

    [Route("tpedu/v1/class-request")]
    [ApiController]
    public class ClassRequestController : ControllerBase
    {
        private readonly IClassRequestService _classRequestService;
        private readonly IStudentProfileService _studentProfileService;
        private readonly ITutorProfileService _tutorProfileService;

        public ClassRequestController(
            IClassRequestService classRequestService,
            IStudentProfileService studentProfileService,
            ITutorProfileService tutorProfileService)
        {
            _classRequestService = classRequestService;
            _studentProfileService = studentProfileService;
            _tutorProfileService = tutorProfileService;
        }

        #region Student & Parent Actions
        /// <summary>
        /// [Student] Tạo một request (direct hoặc marketplace).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> CreateClassRequest([FromBody] CreateClassRequestDto dto)
        {

            try
            {
                var userId = User.RequireUserId(); // Lấy ID từ Token
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                var result = await _classRequestService.CreateClassRequestAsync(userId, role, dto);

                // Trả về 201 Created
                return CreatedAtAction(nameof(GetClassRequestById), new { id = result?.Id }, ApiResponse<ClassRequestResponseDto>.Ok(result));
            }
            catch (ArgumentException ex) // Lỗi thiếu thông tin (vd: Parent không chọn con)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex) // Lỗi không có quyền (không phải con mình)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// [Student] Cập nhật thông tin request (khi còn Pending).
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> UpdateClassRequest(string id, [FromBody] UpdateClassRequestDto dto)
        {
            try
            {
            var userId = User.RequireUserId();
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Student";

            var result = await _classRequestService.UpdateClassRequestAsync(userId, role, id, dto);
            if (result == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu."));

            return Ok(ApiResponse<ClassRequestResponseDto>.Ok(result, "Cập nhật thành công."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex) // Lỗi logic (vd: status != Pending)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// [Student] Cập nhật lịch (schedules) cho một request (khi còn Pending).
        /// </summary>
        [HttpPut("{id}/schedules")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> UpdateClassRequestSchedules(string id, [FromBody] List<ClassRequestScheduleDto> scheduleDtos)
        {

            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Token không hợp lệ." });
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Student";

            await _classRequestService.UpdateClassRequestScheduleAsync(userId, role, id, scheduleDtos);
            return Ok(new { message = "Cập nhật lịch thành công." });
        }

        /// <summary>
        /// [Student] Hủy một request (khi còn Pending).
        /// </summary>
        [HttpPatch("{id}/cancel")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> CancelClassRequest(string id)
        {

            var userId = User.GetUserId();
            if (userId == null)
                    return Unauthorized(new { message = "Token không hợp lệ." });
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Student";

            await _classRequestService.CancelClassRequestAsync(userId, role, id);
                return Ok(new { message = "Hủy yêu cầu thành công." });
        }

        /// <summary>
        /// [Student] Lấy các request CỦA TÔI.
        /// </summary>
        [HttpGet("my-requests")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> GetMyClassRequests([FromQuery] string? childId)
        {
            try
            {
            var userId = User.GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Token không hợp lệ." });
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Student";

            var result = await _classRequestService.GetMyClassRequestsAsync(userId, role, childId);
            return Ok(ApiResponse<IEnumerable<ClassRequestResponseDto>>.Ok(result));

            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }
        #endregion

        #region Tutor Actions
        /// <summary>
        /// [Tutor] Lấy các request "Direct" (gửi thẳng) đến TÔI.
        /// </summary>
        [HttpGet("direct-requests")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetDirectRequests()
        {
            var tutorUserId = User.GetUserId();
            if (tutorUserId == null)
                return Unauthorized(new { message = "Token không hợp lệ." });

            var result = await _classRequestService.GetDirectRequestsAsync(tutorUserId);
            return Ok(result);
        }

        /// <summary>
        /// [Tutor] Phản hồi (Accept/Reject) một request "Direct".
        /// </summary>
        [HttpPatch("{id}/respond")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> RespondToDirectRequest(
            string id, 
            [FromQuery] bool accept,
            [FromBody] UpdateStatusDto? dto = null)
        {

                var tutorUserId = User.GetUserId();
                if (tutorUserId == null)
                    return Unauthorized(new { message = "Token không hợp lệ." });

                var classId = await _classRequestService.RespondToDirectRequestAsync(
                    tutorUserId, 
                    id, 
                    accept,
                    dto?.MeetingLink);
                
                if (accept && !string.IsNullOrEmpty(classId))
                {
                    return Ok(new { 
                        message = "Đã chấp nhận yêu cầu.",
                        classId = classId // Trả về ClassId để học sinh dùng cho thanh toán
                    });
                }
                else
                {
                    return Ok(new { message = "Đã từ chối yêu cầu." });
                }

        }
        #endregion

        #region Public Actions
        /// <summary>
        /// [Public] Lấy các request trên "Marketplace" (có phân trang).
        /// </summary>
        [HttpGet("marketplace")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMarketplaceRequests(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
            [FromQuery] string? subject = null, [FromQuery] string? educationLevel = null,
            [FromQuery] string? mode = null, [FromQuery] string? locationContains = null)
        {
            // Chỉ lấy các request "Active"
            var (data, totalCount) = await _classRequestService.GetMarketplaceRequestsAsync(
                page, pageSize, null, subject, educationLevel, mode, locationContains);

            return Ok(new { TotalCount = totalCount, Data = data });
        }

        /// <summary>
        /// [Public] Lấy chi tiết một request.
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetClassRequestById(string id)
        {
            var result = await _classRequestService.GetClassRequestByIdAsync(id);
            if (result == null)
                return NotFound(new { message = "Không tìm thấy yêu cầu." });
            return Ok(result);
        }
        #endregion

        /// <summary>
        /// [Admin] Cập nhật trạng thái của một request.
        /// </summary>
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateClassRequestStatus(string id, [FromBody] UpdateStatusDto dto)
        {
                await _classRequestService.UpdateClassRequestStatusAsync(id, dto);
                return Ok(new { message = $"Cập nhật trạng thái thành '{dto.Status}' thành công." });
        }
    }
}
