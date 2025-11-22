using BusinessLayer.DTOs.Schedule.AvailabilityBlock;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BusinessLayer.Service.Interface; // Nơi chứa IAvailabilityBlockService
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http; // StatusCodes
using TPEdu_API.Common.Extensions; // .RequireUserId()
using DataLayer.Repositories.GenericType.Abstraction;
using DataLayer.Entities;
using BusinessLayer.Service;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json.Serialization;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/availability-block")]
    [ApiController]
    [Authorize(Roles = "Tutor")] 
    public class AvailabilityBlockController : ControllerBase
    {
        private readonly IAvailabilityBlockService _blockService;
        private readonly ITutorProfileService _tutorProfileService;

        // Inject IAvailabilityBlockService
        public AvailabilityBlockController(
                    IAvailabilityBlockService blockService,
                    ITutorProfileService tutorProfileService) // Fix constructor
        {
            _blockService = blockService;
            _tutorProfileService = tutorProfileService; // Add tutorProfileService
        }

        private async Task<string?> GetTutorIdFromClaims()
        {
            // 1. Lấy UserId từ claim (an toàn)
            var userId = User.RequireUserId();

            // 2. Dùng Service để tra cứu (Sạch sẽ)
            var tutorId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(userId);

            return tutorId; // Service sẽ trả về null nếu không tìm thấy
        }

        [HttpPost("blocks")]
        [ProducesResponseType(typeof(List<AvailabilityBlockDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateAvailabilityBlock([FromBody] CreateAvailabilityBlockDto createDto)
        {
            try
            {
                // Lấy TutorId (đã bao gồm check 401)
                var tutorId = await GetTutorIdFromClaims();
                if (string.IsNullOrEmpty(tutorId))
                {
                    // Check 403: User có đăng nhập (401) nhưng không có Profile
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "User does not have an associated tutor profile." });
                }

                // Gọi service chính
                var createdBlocks = await _blockService.CreateBlockAsync(tutorId, createDto);

                // Trả về 201 Created
                var firstBlockId = createdBlocks.FirstOrDefault()?.Id ?? string.Empty;
                return CreatedAtAction(nameof(GetMyAvailabilityBlocks), new { startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date }, createdBlocks);
            }
            catch (UnauthorizedAccessException ex) // Lỗi 401 từ RequireUserId()
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (ArgumentException ex) // Lỗi 400 validation (vd: EndTime < StartTime)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex) // Lỗi 409 trùng lịch
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex) // Lỗi 500
            {
                Console.WriteLine($"ERROR Creating Block: {ex}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred while creating the availability block." });
            }
        }

        [HttpGet("blocks")]
        [ProducesResponseType(typeof(IEnumerable<AvailabilityBlockDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyAvailabilityBlocks([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            // Đưa validation lên đầu
            if (endDate < startDate)
            {
                return BadRequest(new { message = "End date must be on or after start date." });
            }

            try
            {
                // Lấy TutorId (check 401)
                var tutorId = await GetTutorIdFromClaims();
                if (string.IsNullOrEmpty(tutorId))
                {
                    // Check 403
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "User does not have an associated tutor profile." });
                }

                // Sửa lỗi copy-paste: Gọi đúng hàm GetBlocksByTutorAsync
                var blocks = await _blockService.GetBlocksByTutorAsync(tutorId, startDate, endDate);
                return Ok(blocks);
            }
            catch (UnauthorizedAccessException ex) // Lỗi 401 từ RequireUserId()
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex) // Lỗi 500
            {
                Console.WriteLine($"ERROR Getting Blocks: {ex}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred while retrieving availability blocks." });
            }
        }

        [HttpDelete("blocks/{blockId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAvailabilityBlock(string blockId)
        {
            // Đưa validation lên đầu
            if (!Guid.TryParse(blockId, out _))
            {
                return BadRequest(new { message = "Invalid block ID format." });
            }

            try
            {
                // Lấy TutorId (check 401)
                var tutorId = await GetTutorIdFromClaims();
                if (string.IsNullOrEmpty(tutorId))
                {
                    // Check 403
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "User does not have an associated tutor profile." });
                }

                // Gọi service chính
                var success = await _blockService.DeleteBlockAsync(blockId, tutorId);

                if (success)
                {
                    return NoContent(); // 204
                }
                else
                {
                    // 404: Không tìm thấy block ID HOẶC block này không phải của bạn
                    return NotFound(new { message = $"Availability block with ID '{blockId}' not found or you do not have permission to delete it." });
                }
            }
            catch (UnauthorizedAccessException ex) // Lỗi 401 từ RequireUserId()
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex) // Lỗi 500
            {
                Console.WriteLine($"ERROR Deleting Block {blockId}: {ex.Message} --- InnerException: {ex.InnerException?.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred while deleting the availability block." });
            }
        }

        // --- (Tùy chọn) Endpoint để lấy chi tiết 1 block ---
        // [HttpGet("blocks/{id}")]
        // public async Task<IActionResult> GetBlockById(string id) { ... }
    }
}