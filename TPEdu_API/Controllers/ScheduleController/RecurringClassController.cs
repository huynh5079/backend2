using BusinessLayer.DTOs.Schedule.ClassSchedule;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/recurringclass")]
    [ApiController]
    [Authorize(Roles = "Tutor")]
    public class ClassScheduleController : ControllerBase
    {
        private readonly IClassScheduleService _recurringClassService;
        private readonly ITutorProfileService _tutorProfileService;

        public ClassScheduleController(
                    IClassScheduleService recurringClassService,
                    ITutorProfileService tutorProfileService)
        {
            _recurringClassService = recurringClassService;
            _tutorProfileService = tutorProfileService;
        }

        private async Task<string?> GetTutorIdFromClaims()
        {
            var userId = User.RequireUserId(); // Take UserId from claim "uid"
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(userId);
            return tutorProfileId;
        }

        /// <summary>
        /// Create a class wwith recurring schedule (no assign).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateRecurringClass([FromBody] CreateClassScheduleDto createDto)
        {
            try
            {
                var tutorProfileId = await GetTutorIdFromClaims();
                if (string.IsNullOrEmpty(tutorProfileId))
                {
                    // User role Tutor but no profile -> 403 Forbidden
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Người dùng chưua được cấp phép làm gia sư." });
                }

                // Call with corect TutorProfile.Id
                var createdClass = await _recurringClassService.CreateRecurringClassScheduleAsync(tutorProfileId, createDto);

                return CreatedAtAction(nameof(GetClassById_Placeholder), new { id = createdClass.Id }, createdClass);
            }
            catch (UnauthorizedAccessException ex) // 401 RequireUserId()
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi tạo máy chủ khi tạo lớp học: {ex.Message} --- InnerException: {ex.InnerException?.Message}"); // Log chi tiết hơn
                return StatusCode(500, new { message = "Lỗi máy chủ khi tạo lớp học." });
            }
        }

        // Temporary endpoint
        [HttpGet("{id}")]
        [AllowAnonymous]
        public IActionResult GetClassById_Placeholder(string id)
        {
            return Ok(new { message = $"Endpoint GetClassById({id}) chưa được triển khai." });
        }
    }
}