using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.ClassRequest;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        /// <summary>
        /// [Student] Tạo một request (direct hoặc marketplace).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CreateClassRequest([FromBody] CreateClassRequestDto dto)
        {
            var studentUserId = User.RequireUserId();

            var result = await _classRequestService.CreateClassRequestAsync(studentUserId, dto);

            if (result == null)
            {
                return StatusCode(500, ApiResponse<object>.Fail("Lỗi không xác định khi tạo yêu cầu."));
            }

            return CreatedAtAction(nameof(GetClassRequestById), new { id = result.Id }, ApiResponse<ClassRequestResponseDto>.Ok(result));
        }

        /// <summary>
        /// [Student] Cập nhật thông tin request (khi còn Pending).
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> UpdateClassRequest(string id, [FromBody] UpdateClassRequestDto dto)
        {

                var studentUserId = User.GetUserId();
                if (studentUserId == null)
                    return Unauthorized(new { message = "Token không hợp lệ." });

                var result = await _classRequestService.UpdateClassRequestAsync(studentUserId, id, dto);
                return Ok(result);

        }

        /// <summary>
        /// [Student] Cập nhật lịch (schedules) cho một request (khi còn Pending).
        /// </summary>
        [HttpPut("{id}/schedules")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> UpdateClassRequestSchedules(string id, [FromBody] List<ClassRequestScheduleDto> scheduleDtos)
        {

                var studentUserId = User.GetUserId();
                if (studentUserId == null)
                    return Unauthorized(new { message = "Token không hợp lệ." });

                var success = await _classRequestService.UpdateClassRequestScheduleAsync(studentUserId, id, scheduleDtos);
                return Ok(new { message = "Cập nhật lịch thành công." });

        }

        /// <summary>
        /// [Student] Hủy một request (khi còn Pending).
        /// </summary>
        [HttpPatch("{id}/cancel")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelClassRequest(string id)
        {

                var studentUserId = User.GetUserId();
                if (studentUserId == null)
                    return Unauthorized(new { message = "Token không hợp lệ." });

                await _classRequestService.CancelClassRequestAsync(studentUserId, id);
                return Ok(new { message = "Hủy yêu cầu thành công." });

        }

        /// <summary>
        /// [Student] Lấy các request CỦA TÔI.
        /// </summary>
        [HttpGet("my-requests")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyClassRequests()
        {
            var studentUserId = User.GetUserId();
            if (studentUserId == null)
                return Unauthorized(new { message = "Token không hợp lệ." });

            var result = await _classRequestService.GetMyClassRequestsAsync(studentUserId);
            return Ok(result);
        }

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
        public async Task<IActionResult> RespondToDirectRequest(string id, [FromQuery] bool accept)
        {

                var tutorUserId = User.GetUserId();
                if (tutorUserId == null)
                    return Unauthorized(new { message = "Token không hợp lệ." });

                await _classRequestService.RespondToDirectRequestAsync(tutorUserId, id, accept);
                return Ok(new { message = $"Đã {(accept ? "chấp nhận" : "từ chối")} yêu cầu." });

        }

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
