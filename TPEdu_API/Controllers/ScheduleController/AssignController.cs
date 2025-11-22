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
using System.Threading.Tasks;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/assigns")]
    [ApiController]
    [Authorize(Roles = "Student,Parent")] // Only allow Students and Parents
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
        public async Task<IActionResult> AssignStudentToClass([FromBody] AssignRecurringClassDto assignDto)
        {
            // Take userId from JWT token
            var userId = User.RequireUserId();
            var updatedClass = await _assignService.AssignRecurringClassAsync(userId, assignDto.ClassId);

            // Return response
            return Ok(ApiResponse<ClassDto>.Ok(updatedClass));
        }

        /// <summary>
        /// [Student] Withdraw from a class.
        /// </summary>
        [HttpDelete("{classId}")]
        public async Task<IActionResult> WithdrawFromClass(string classId)
        {
            var userId = User.RequireUserId();

            await _assignService.WithdrawFromClassAsync(userId, classId);

            return Ok(ApiResponse<object>.Ok(null, "Rút khỏi lớp thành công."));
        }

        /// <summary>
        /// [Student] Get list of classes the student has enrolled in.
        /// </summary>
        [HttpGet("my-classes")]
        public async Task<IActionResult> GetMyEnrolledClasses()
        {
            var userId = User.RequireUserId();
            var enrolledClasses = await _assignService.GetMyEnrolledClassesAsync(userId);

            return Ok(ApiResponse<List<MyEnrolledClassesDto>>.Ok(enrolledClasses, "Lấy danh sách lớp đã ghi danh thành công."));
        }

        /// <summary>
        /// [Student] Check if student has enrolled in a specific class.
        /// </summary>
        [HttpGet("{classId}/check")]
        public async Task<IActionResult> CheckEnrollment(string classId)
        {
            var userId = User.RequireUserId();
            var enrollmentCheck = await _assignService.CheckEnrollmentAsync(userId, classId);

            var message = enrollmentCheck.IsEnrolled 
                ? "Học sinh đã vào lớp này." 
                : "Học sinh chưa vào lớp này.";

            return Ok(ApiResponse<EnrollmentCheckDto>.Ok(enrollmentCheck, message));
        }

        /// <summary>
        /// [Student/Parent] Get enrollment detail for a specific class.
        /// </summary>
        [HttpGet("{classId}/detail")]
        public async Task<IActionResult> GetEnrollmentDetail(string classId)
        {
            var userId = User.RequireUserId();
            var enrollmentDetail = await _assignService.GetEnrollmentDetailAsync(userId, classId);
            return Ok(ApiResponse<ClassAssignDetailDto>.Ok(enrollmentDetail, "Lấy thông tin enrollment thành công."));
        }
    }
}