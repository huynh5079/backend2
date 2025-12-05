using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/admin/classes")]
    [Authorize(Roles = "Admin")]
    public class AdminClassController : ControllerBase
    {
        private readonly IClassService _classService;

        public AdminClassController(IClassService classService)
        {
            _classService = classService;
        }

        /// <summary>
        /// Admin hủy lớp học (toàn bộ lớp)
        /// </summary>
        [HttpPost("{classId}/cancel")]
        public async Task<IActionResult> CancelClass(string classId, [FromBody] CancelClassRequestBodyDto requestBody)
        {
            try
            {
                var adminUserId = User.GetUserId();
                if (string.IsNullOrEmpty(adminUserId))
                    return Unauthorized(new { message = "Token không hợp lệ." });

                // Map từ body DTO sang service DTO với ClassId từ route
                var request = new CancelClassRequestDto
                {
                    ClassId = classId,
                    Reason = requestBody.Reason,
                    Note = requestBody.Note,
                    StudentId = requestBody.StudentId
                };

                var result = await _classService.CancelClassByAdminAsync(adminUserId, request);
                
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    data = new
                    {
                        classId = result.ClassId,
                        newStatus = result.NewStatus.ToString(),
                        reason = result.Reason.ToString(),
                        refundedEscrowsCount = result.RefundedEscrowsCount,
                        totalRefundedAmount = result.TotalRefundedAmount,
                        depositRefunded = result.DepositRefunded,
                        depositRefundAmount = result.DepositRefundAmount
                    }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        /// <summary>
        /// Admin hủy 1 học sinh khỏi lớp (group class)
        /// </summary>
        [HttpPost("{classId}/students/{studentId}/cancel")]
        public async Task<IActionResult> CancelStudentEnrollment(
            string classId, 
            string studentId, 
            [FromBody] CancelStudentEnrollmentRequestDto request)
        {
            try
            {
                var adminUserId = User.GetUserId();
                if (string.IsNullOrEmpty(adminUserId))
                    return Unauthorized(new { message = "Token không hợp lệ." });

                var result = await _classService.CancelStudentEnrollmentAsync(
                    adminUserId, 
                    classId, 
                    studentId, 
                    request.Reason, 
                    request.Note);
                
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    data = new
                    {
                        classId = result.ClassId,
                        newStatus = result.NewStatus.ToString(),
                        reason = result.Reason.ToString(),
                        refundedEscrowsCount = result.RefundedEscrowsCount,
                        totalRefundedAmount = result.TotalRefundedAmount
                    }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }

    /// <summary>
    /// DTO cho cancel student enrollment request
    /// </summary>
    public class CancelStudentEnrollmentRequestDto
    {
        public ClassCancelReason Reason { get; set; }
        public string? Note { get; set; }
    }
}

