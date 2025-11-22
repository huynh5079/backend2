using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Attendance;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/attendance")]
    [Authorize]
    public class AttendanceController : ControllerBase
    {
        private readonly IAttendanceService _svc;
        public AttendanceController(IAttendanceService svc) => _svc = svc;

        // Tutor mark single
        [HttpPost("mark")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> Mark([FromBody] MarkAttendanceRequest req)
        {
            var tutorId = User.RequireUserId();
            var dto = await _svc.MarkAsync(tutorId, req);
            return Ok(ApiResponse<object>.Ok(dto, "điểm danh thành công"));
        }

        // Tutor mark bulk
        [HttpPost("mark-bulk/{lessonId}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> MarkBulk(string lessonId, [FromBody] BulkMarkRequest body)
        {
            var tutorId = User.RequireUserId();
            var rs = await _svc.BulkMarkForLessonAsync(tutorId, lessonId, body.StudentStatus, body.Notes);
            return Ok(ApiResponse<object>.Ok(rs, "điểm danh hàng loạt thành công"));
        }

        // Tutor xem danh sách 1 buổi
        [HttpGet("lesson/{lessonId}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetLessonAttendance(string lessonId)
        {
            var tutorId = User.RequireUserId();
            var list = await _svc.GetLessonAttendanceAsync(tutorId, lessonId);
            return Ok(ApiResponse<object>.Ok(list, "lấy danh sách thành công"));
        }

        // Calendar overlay - Tutor
        [HttpGet("calendar/tutor")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> TutorCalendar([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var tutorId = User.RequireUserId();
            var data = await _svc.GetTutorAttendanceOnCalendarAsync(tutorId, start, end);
            return Ok(ApiResponse<object>.Ok(data, "ok"));
        }

        // Calendar overlay - Student
        [HttpGet("calendar/student")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StudentCalendar([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var studentUserId = User.RequireUserId();
            var data = await _svc.GetStudentAttendanceOnCalendarAsync(studentUserId, start, end);
            return Ok(ApiResponse<object>.Ok(data, "ok"));
        }

        // Calendar overlay - Parent xem con
        [HttpGet("calendar/child/{studentId}")]
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> ParentChildCalendar(string studentId, [FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var parentUserId = User.RequireUserId();
            var data = await _svc.GetParentChildAttendanceCalendarAsync(parentUserId, studentId, start, end);
            return Ok(ApiResponse<object>.Ok(data, "ok"));
        }

        [HttpGet("lesson/{lessonId}/roster")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetLessonRoster(string lessonId)
        {
            var tutorId = User.RequireUserId();
            var data = await _svc.GetLessonRosterForTutorAsync(tutorId, lessonId);
            return Ok(ApiResponse<object>.Ok(data, "lấy danh sách học sinh của buổi học thành công"));
        }
    }
}
