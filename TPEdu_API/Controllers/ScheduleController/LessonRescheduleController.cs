using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.RescheduleRequest;
using BusinessLayer.Helper;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers.ScheduleController
{
    [ApiController]
    [Route("tpedu/v1/lessons/reschedule")]
    [Authorize]
    public class LessonRescheduleController : ControllerBase
    {
        private readonly ILessonRescheduleService _rescheduleService;

        public LessonRescheduleController(ILessonRescheduleService rescheduleService)
        {
            _rescheduleService = rescheduleService;
        }

        /// <summary>
        /// (Tutor) Gửi yêu cầu đổi lịch
        /// </summary>
        [HttpPost("{lessonId}/request")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> CreateRequest([FromBody] CreateRescheduleRequestDto dto, string lessonId)
        {
            var tutorUserId = User.RequireUserId();
            var result = await _rescheduleService.CreateRequestAsync(tutorUserId, lessonId, dto);
            return Ok(ApiResponse<RescheduleRequestDto>.Ok(result, "Đã gửi yêu cầu đổi lịch."));
        }

        /// <summary>
        /// (Student/Parent) Gửi yêu cầu đổi lịch tới gia sư
        /// </summary>
        [HttpPost("{lessonId}/student-request")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> CreateRequestByStudent([FromBody] CreateRescheduleRequestDto dto, string lessonId)
        {
            var actorUserId = User.RequireUserId();
            var result = await _rescheduleService.CreateRequestByStudentAsync(actorUserId, lessonId, dto);
            return Ok(ApiResponse<RescheduleRequestDto>.Ok(result, "Đã gửi yêu cầu đổi lịch tới gia sư."));
        }

        [HttpPatch("{requestId}/accept")]
        [Authorize(Roles = "Tutor,Student,Parent")]
        public async Task<IActionResult> AcceptRequest(string requestId)
        {
            var actorUserId = User.RequireUserId();
            var result = await _rescheduleService.AcceptRequestAsync(actorUserId, requestId);
            return Ok(ApiResponse<RescheduleRequestDto>.Ok(result, "Đã chấp nhận đổi lịch."));
        }

        /// <summary>
        /// Từ chối yêu cầu đổi lịch
        /// </summary>
        [HttpPatch("{requestId}/deny")]
        [Authorize(Roles = "Tutor,Student,Parent")]
        public async Task<IActionResult> DenyRequest(string requestId)
        {
            var actorUserId = User.RequireUserId();
            var result = await _rescheduleService.RejectRequestAsync(actorUserId, requestId);
            return Ok(ApiResponse<RescheduleRequestDto>.Ok(result, "Đã từ chối đổi lịch."));
        }

        /// <summary>
        /// (Tutor/Student/Parent) Lấy danh sách các yêu cầu đang chờ
        /// </summary>
        [HttpGet("pending-requests")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var actorUserId = User.RequireUserId();
            var result = await _rescheduleService.GetPendingRequestsAsync(actorUserId);
            return Ok(ApiResponse<IEnumerable<RescheduleRequestDto>>.Ok(result));
        }
    }
}
