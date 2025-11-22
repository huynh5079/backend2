using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Feedback;
using BusinessLayer.Helper;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/feedbacks")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;
        public FeedbacksController(IFeedbackService service) => _service = service;

        /// <summary>
        /// (1) Tạo feedback cho BUỔI HỌC (lesson-only)
        /// </summary>
        [HttpPost]
        [Authorize] // Tutor/Student/Parent
        public async Task<IActionResult> Create([FromBody] CreateFeedbackRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<FeedbackDto>.Fail("Dữ liệu không hợp lệ"));

            try
            {
                var actorUserId = User.RequireUserId();
                var dto = await _service.CreateAsync(actorUserId, req);
                return Ok(ApiResponse<FeedbackDto>.Ok(dto, "Tạo feedback thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<FeedbackDto>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// (2) Tạo feedback CÔNG KHAI trên TRANG TUTOR (không gắn lesson)
        /// </summary>
        [HttpPost("tutors/{tutorUserId}")]
        [Authorize]
        public async Task<IActionResult> CreateForTutorProfile(
            [FromRoute] string tutorUserId,
            [FromBody] CreateTutorProfileFeedbackRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<FeedbackDto>.Fail("Dữ liệu không hợp lệ"));

            try
            {
                var actorUserId = User.RequireUserId();
                var dto = await _service.CreateForTutorProfileAsync(actorUserId, tutorUserId, req);
                return Ok(ApiResponse<FeedbackDto>.Ok(dto, "Tạo feedback công khai thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<FeedbackDto>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// (3) Cập nhật feedback (lesson hoặc public tuỳ theo feedbackId)
        /// </summary>
        [HttpPut("{feedbackId}")]
        [Authorize]
        public async Task<IActionResult> Update(
            [FromRoute] string feedbackId,
            [FromBody] UpdateFeedbackRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<FeedbackDto>.Fail("Dữ liệu không hợp lệ"));

            try
            {
                var actorUserId = User.RequireUserId();
                var dto = await _service.UpdateAsync(actorUserId, feedbackId, req);
                return Ok(ApiResponse<FeedbackDto>.Ok(dto, "Cập nhật feedback thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<FeedbackDto>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// (4) List feedback theo LỚP HỌC (class-only hiển thị ở trang lớp/buổi học)
        /// </summary>
        [HttpGet("lesson/{classId}")]
        [Authorize]
        public async Task<IActionResult> GetByClass([FromRoute] string classId)
        {
            try
            {
                var items = await _service.GetClassFeedbacksAsync(classId);
                return Ok(ApiResponse<IEnumerable<FeedbackDto>>.Ok(items));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<IEnumerable<FeedbackDto>>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// (5) List feedback PUBLIC trên TRANG TUTOR
        /// </summary>
        [HttpGet("tutors/{tutorUserId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByTutor(
            [FromRoute] string tutorUserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var (items, total) = await _service.GetTutorFeedbacksAsync(tutorUserId, page, pageSize);
                var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);

                var data = new
                {
                    items,
                    paging = new
                    {
                        page,
                        pageSize,
                        total,
                        totalPages
                    }
                };

                return Ok(ApiResponse<object>.Ok(data));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// (6) Rating tổng hợp cho TUTOR (dùng cho header / overview)
        /// </summary>
        [HttpGet("tutors/{tutorUserId}/rating")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTutorRating([FromRoute] string tutorUserId)
        {
            try
            {
                var sum = await _service.GetTutorRatingAsync(tutorUserId);
                return Ok(ApiResponse<TutorRatingSummaryDto>.Ok(sum));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<TutorRatingSummaryDto>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// (7) Xoá feedback
        /// </summary>
        [HttpDelete("{feedbackId}")]
        [Authorize]
        public async Task<IActionResult> Delete([FromRoute] string feedbackId)
        {
            try
            {
                var actorUserId = User.RequireUserId();
                var ok = await _service.DeleteAsync(actorUserId, feedbackId);

                if (ok)
                    return Ok(ApiResponse<object>.Ok(new { success = true }, "Đã xoá feedback"));

                return BadRequest(ApiResponse<object>.Fail("Xoá feedback thất bại"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }
    }
}
