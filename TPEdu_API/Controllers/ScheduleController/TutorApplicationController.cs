using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.TutorApplication;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/tutor-application")]
    [ApiController]
    [Authorize] // Require login for all
    public class TutorApplicationController : ControllerBase
    {
        private readonly ITutorApplicationService _tutorApplicationService;

        public TutorApplicationController(ITutorApplicationService tutorApplicationService)
        {
            _tutorApplicationService = tutorApplicationService;
        }
        #region Tutor action
        /// <summary>
        /// [Tutor] Apply for a ClassRequest (on Marketplace)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> CreateApplication([FromBody] CreateTutorApplicationDto dto)
        {
            var tutorUserId = User.RequireUserId();
            var result = await _tutorApplicationService.CreateApplicationAsync(tutorUserId, dto);

            // Return 201 Created
            return CreatedAtAction(
                nameof(GetApplicationsForRequest), // Temporarily link to Get function (of Student)
                new { classRequestId = result.ClassRequestId },
                ApiResponse<TutorApplicationResponseDto>.Ok(result));
        }

        /// <summary>
        /// [Tutor] Withdraw application (while still Active)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> WithdrawApplication(string id)
        {
            var tutorUserId = User.RequireUserId();
            await _tutorApplicationService.WithdrawApplicationAsync(tutorUserId, id);
            return Ok(ApiResponse<object>.Ok(null, "Đã rút đơn ứng tuyển thành công."));
        }

        /// <summary>
        /// [Tutor] Get the applications I have applied for
        /// </summary>
        [HttpGet("my-applications")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetMyApplications()
        {
            var tutorUserId = User.RequireUserId();
            var result = await _tutorApplicationService.GetMyApplicationsAsync(tutorUserId);
            return Ok(ApiResponse<IEnumerable<TutorApplicationResponseDto>>.Ok(result));
        }
        #endregion

        #region Student parent action
        /// <summary>
        /// [Student] Get the list of candidates for MY ClassRequest
        /// </summary>
        [HttpGet("request/{classRequestId}")]       
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> GetApplicationsForRequest(string classRequestId)
        {
            try
            {
                var userId = User.RequireUserId();
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                var result = await _tutorApplicationService.GetApplicationsForMyRequestAsync(userId, role, classRequestId);
                return Ok(ApiResponse<IEnumerable<TutorApplicationResponseDto>>.Ok(result));
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
        /// [Student] Accepting an application
        /// </summary>
        [HttpPatch("{id}/accept")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> AcceptApplication(string id)
        {
            var userId = User.RequireUserId();
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            await _tutorApplicationService.AcceptApplicationAsync(userId, role, id);
            return Ok(ApiResponse<object>.Ok(null, "Chấp nhận gia sư thành công. Lớp học đã được tạo."));
        }

        /// <summary>
        /// [Student] Rejecting an application
        /// </summary>
        [HttpPatch("{id}/reject")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> RejectApplication(string id)
        {
            var userId = User.RequireUserId();
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

            await _tutorApplicationService.RejectApplicationAsync(userId, role, id);
            return Ok(ApiResponse<object>.Ok(null, "Đã từ chối đơn ứng tuyển."));
        }
        #endregion
    }
}