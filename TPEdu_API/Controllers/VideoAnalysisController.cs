using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.VideoAnalysis;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/lessons/{lessonId}/materials/{mediaId}/analysis")]
    [Authorize]
    public class VideoAnalysisController : ControllerBase
    {
        private readonly IVideoAnalysisService _videoAnalysisService;
        private readonly DataLayer.Repositories.Abstraction.IUnitOfWork _uow;

        public VideoAnalysisController(
            IVideoAnalysisService videoAnalysisService,
            DataLayer.Repositories.Abstraction.IUnitOfWork uow)
        {
            _videoAnalysisService = videoAnalysisService;
            _uow = uow;
        }

        /// <summary>
        /// Lấy kết quả phân tích video
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAnalysis(string lessonId, string mediaId)
        {
            // Kiểm tra quyền truy cập
            var userId = User.RequireUserId();
            await CheckAccessPermissionAsync(userId, lessonId);

            var analysis = await _videoAnalysisService.GetAnalysisAsync(mediaId);
            if (analysis == null)
                return NotFound(ApiResponse<object>.Fail("Chưa có kết quả phân tích cho video này."));

            return Ok(ApiResponse<object>.Ok(analysis, "Lấy kết quả phân tích thành công"));
        }

        /// <summary>
        /// Trigger phân tích video (hoặc phân tích lại)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> AnalyzeVideo(string lessonId, string mediaId, CancellationToken ct)
        {
            var userId = User.RequireUserId();
            await CheckAccessPermissionAsync(userId, lessonId, isTutor: true);

            // Lấy media info
            var media = await _uow.Media.GetByIdAsync(mediaId);
            if (media == null || media.LessonId != lessonId)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy video."));

            if (string.IsNullOrEmpty(media.FileUrl))
                return BadRequest(ApiResponse<object>.Fail("Video không có URL hợp lệ."));

            try
            {
                // Tạo hoặc update analysis record với status Processing trước
                var existing = await _uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
                VideoAnalysis analysis;
                
                if (existing != null)
                {
                    analysis = existing;
                    analysis.Status = VideoAnalysisStatus.Processing;
                    analysis.UpdatedAt = DateTime.Now;
                    await _uow.VideoAnalyses.UpdateAsync(analysis);
                }
                else
                {
                    analysis = new VideoAnalysis
                    {
                        MediaId = mediaId,
                        LessonId = lessonId,
                        Status = VideoAnalysisStatus.Processing
                    };
                    await _uow.VideoAnalyses.CreateAsync(analysis);
                }
                await _uow.SaveChangesAsync();

                // Chạy phân tích trong background để không block response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _videoAnalysisService.AnalyzeVideoAsync(mediaId, lessonId, media.FileUrl, ct);
                    }
                    catch (Exception ex)
                    {
                        // Log error và update status
                        Console.WriteLine($"Error analyzing video {mediaId}: {ex.Message}");
                        var failedAnalysis = await _uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
                        if (failedAnalysis != null)
                        {
                            failedAnalysis.Status = VideoAnalysisStatus.Failed;
                            failedAnalysis.ErrorMessage = ex.Message;
                            failedAnalysis.UpdatedAt = DateTime.Now;
                            await _uow.VideoAnalyses.UpdateAsync(failedAnalysis);
                            await _uow.SaveChangesAsync();
                        }
                    }
                }, ct);

                // Trả về ngay với status Processing
                var resultDto = new VideoAnalysisDto
                {
                    Id = analysis.Id,
                    MediaId = analysis.MediaId,
                    LessonId = analysis.LessonId,
                    Status = analysis.Status.ToString(),
                    CreatedAt = analysis.CreatedAt,
                    UpdatedAt = analysis.UpdatedAt
                };
                
                return Ok(ApiResponse<object>.Ok(resultDto, "Đã bắt đầu phân tích video. Vui lòng chờ..."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail($"Lỗi khi phân tích video: {ex.Message}"));
            }
        }

        /// <summary>
        /// Hỏi câu hỏi về video
        /// </summary>
        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestion(
            string lessonId,
            string mediaId,
            [FromBody] VideoQuestionRequestDto request,
            CancellationToken ct)
        {
            // Kiểm tra quyền truy cập
            var userId = User.RequireUserId();
            await CheckAccessPermissionAsync(userId, lessonId);

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(ApiResponse<object>.Fail("Câu hỏi không được để trống."));

            try
            {
                var response = await _videoAnalysisService.AnswerQuestionAsync(mediaId, request, ct);
                return Ok(ApiResponse<object>.Ok(response, "Trả lời câu hỏi thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail($"Lỗi khi trả lời câu hỏi: {ex.Message}"));
            }
        }

        #region Helper Methods

        private async Task CheckAccessPermissionAsync(string userId, string lessonId, bool isTutor = false)
        {
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);

            if (isTutor)
            {
                // Kiểm tra quyền tutor
                var tutorUserId = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
                if (tutorUserId != userId)
                    throw new UnauthorizedAccessException("Chỉ gia sư của lớp mới có quyền thực hiện thao tác này.");
            }
            else
            {
                // Kiểm tra quyền xem (tutor hoặc student)
                var tutorUserId = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
                var isTutorOwner = tutorUserId == userId;

                if (!isTutorOwner)
                {
                    // Thử check quyền Student
                    var studentProfileId = await _uow.StudentProfiles.GetIdByUserIdAsync(userId);
                    if (studentProfileId != null)
                    {
                        var isApproved = await _uow.ClassAssigns.IsApprovedAsync(cls.Id, studentProfileId);
                        if (!isApproved)
                            throw new UnauthorizedAccessException("Bạn không có quyền truy cập.");
                    }
                    else
                    {
                        throw new UnauthorizedAccessException("Bạn không có quyền truy cập.");
                    }
                }
            }
        }

        #endregion
    }
}

