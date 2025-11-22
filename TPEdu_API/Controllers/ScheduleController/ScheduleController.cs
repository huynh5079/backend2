using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.ScheduleEntry;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;
using System.Security.Claims;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/schedules")]
    [ApiController]
    [Authorize]
    public class ScheduleController : ControllerBase
    {
        private readonly IScheduleViewService _scheduleViewService;

        public ScheduleController(IScheduleViewService scheduleViewService)
        {
            _scheduleViewService = scheduleViewService;
        }

        #region GET
        [HttpGet("tutor/{tutorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTutorSchedule(
            string tutorId, 
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate,
            [FromQuery] string? entryType,
            [FromQuery] string? classId)
        {
            if (endDate < startDate)
            {
                return BadRequest(ApiResponse<object>.Fail("Ngày kết thúc phải sau ngày bắt đầu."));
            }

            try
            {
                var schedule = await _scheduleViewService.GetTutorScheduleAsync(tutorId, startDate, endDate, entryType);
                return Ok(ApiResponse<IEnumerable<ScheduleEntryDto>>.Ok(schedule));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        //--- Get Student Schedule ---

        /// <summary>
        /// [Student/Parent] take student's schedule between startDate and endDate
        /// </summary>
        [HttpGet("my-schedule")]
        [Authorize(Roles = "Student,Parent")] // only Student and Parent can access
        public async Task<IActionResult> GetMySchedule([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            if (endDate < startDate)
            {
                return BadRequest(ApiResponse<object>.Fail("Ngày kết thúc phải sau ngày bắt đầu."));
            }

            try
            {
                // take studentUserId from JWT token
                var studentUserId = User.RequireUserId();

                var schedule = await _scheduleViewService.GetStudentScheduleAsync(studentUserId, startDate, endDate);
                return Ok(ApiResponse<IEnumerable<ScheduleEntryDto>>.Ok(schedule));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }
        #endregion
    }
}