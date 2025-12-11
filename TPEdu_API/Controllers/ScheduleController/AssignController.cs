using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.DTOs.Schedule.ClassAssign;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/assigns")]
    [ApiController]
    public class AssignController : ControllerBase
    {
        private readonly IAssignService _assignService;
        private readonly IStudentProfileService _studentProfileService;

        // Constructor
        public AssignController(IAssignService assignService, IStudentProfileService studentProfileService)
        {
            _assignService = assignService;
            _studentProfileService = studentProfileService;
        }
        /// <summary>
        /// [Student] Assign the student to a class.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Student,Parent")] // Only allow Students and Parents
        public async Task<IActionResult> AssignStudentToClass([FromBody] AssignRecurringClassDto assignDto)
        {
            // Take userId from JWT token
            try
            {
                var userId = User.RequireUserId();
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                var result = await _assignService.AssignRecurringClassAsync(userId, role, assignDto);

                return Ok(ApiResponse<ClassAssignDetailDto>.Ok(result, "Đăng ký lớp học và thanh toán thành công!"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex) // Logic errors (money, class full, schedule conflict)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// [Student] Withdraw from a class.
        /// </summary>
        [HttpDelete("{classId}")]
        [Authorize(Roles = "Student,Parent")] // Only allow Students and Parents
        public async Task<IActionResult> WithdrawFromClass(string classId, [FromQuery] string? studentId)
        {
            try
            {
                var userId = User.RequireUserId();
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                await _assignService.WithdrawFromClassAsync(userId, role, classId, studentId);
                return Ok(ApiResponse<object>.Ok(null, "Rút khỏi lớp thành công."));

            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// [Student] Get list of classes the student has enrolled in.
        /// </summary>
        [HttpGet("my-classes")]
        [Authorize(Roles = "Student,Parent")] // Only allow Students and Parents
        public async Task<IActionResult> GetMyEnrolledClasses([FromQuery] string? childId)
        {
            try
            {
                var userId = User.RequireUserId();
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                var result = await _assignService.GetMyEnrolledClassesAsync(userId, role, childId);
                return Ok(ApiResponse<List<MyEnrolledClassesDto>>.Ok(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// [Student] Check if student has enrolled in a specific class.
        /// </summary>
        [HttpGet("check-enrollment/{classId}")]
        [Authorize(Roles = "Student,Parent")] // Only allow Students and Parents
        public async Task<IActionResult> CheckEnrollment(string classId, [FromQuery] string? studentId)
        {
            try
            {
                var userId = User.RequireUserId();
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

                var result = await _assignService.CheckEnrollmentAsync(userId, role, classId, studentId);

                return Ok(ApiResponse<EnrollmentCheckDto>.Ok(result));
            }
            catch (ArgumentException ex)
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
        /// [Student/Parent] Get enrollment detail for a specific class.
        /// </summary>
        [HttpGet("{classId}/detail")]
        [Authorize(Roles = "Student,Parent,Tutor")]
        public async Task<IActionResult> GetEnrollmentDetail(string classId)
        {
            var userId = User.RequireUserId();
            var enrollmentDetail = await _assignService.GetEnrollmentDetailAsync(userId, classId);
            return Ok(ApiResponse<ClassAssignDetailDto>.Ok(enrollmentDetail, "Lấy thông tin enrollment thành công."));
        }

        // Filter students by tutor and class

        ///// <summary>
        ///// [Tutor] Take all tutor this student assigned to these tutor's classes.
        ///// </summary>
        [HttpGet("my-tutors")]
        [Authorize(Roles = "Student,Parent")] // Only allow Students and Parents
        public async Task<IActionResult> GetMyTutors([FromQuery] string? childId)
        {
            try
            {
                var userId = User.RequireUserId();
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                var result = await _assignService.GetMyTutorsAsync(userId, role, childId);
                return Ok(ApiResponse<List<RelatedResourceDto>>.Ok(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }

        ///// <summary>
        ///// [Tutor] Take all students assigned to this tutor's classes.
        ///// </summary>
        [HttpGet("my-students")]
        [Authorize(Roles = "Tutor")] // Only Tutor can call
        public async Task<IActionResult> GetMyStudents()
        {
            var userId = User.GetUserId(); // take from Claims
            var result = await _assignService.GetMyStudentsAsync(userId);
            return Ok(ApiResponse<List<RelatedResourceDto>>.Ok(result));
        }
    }
}