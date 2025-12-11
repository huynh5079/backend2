using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.VideoAnalysis;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<VideoAnalysisController> _logger;

        public VideoAnalysisController(
            IVideoAnalysisService videoAnalysisService,
            DataLayer.Repositories.Abstraction.IUnitOfWork uow,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<VideoAnalysisController> logger)
        {
            _videoAnalysisService = videoAnalysisService;
            _uow = uow;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Lấy kết quả phân tích video (chỉ học sinh và phụ huynh)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> GetAnalysis(string lessonId, string mediaId)
        {
            try
            {
                // Kiểm tra quyền truy cập
                var userId = User.RequireUserId();
                await CheckAccessPermissionAsync(userId, lessonId);

                var analysis = await _videoAnalysisService.GetAnalysisAsync(mediaId);
                if (analysis == null)
                    return NotFound(ApiResponse<object>.Fail("Chưa có kết quả phân tích cho video này."));

                return Ok(ApiResponse<object>.Ok(analysis, "Lấy kết quả phân tích thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"GetAnalysis - Unauthorized: {ex.Message}");
                return Unauthorized(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GetAnalysis - Error: {ex.Message}");
                return BadRequest(ApiResponse<object>.Fail($"Lỗi: {ex.Message}"));
            }
        }

        /// <summary>
        /// Trigger phân tích video (hoặc phân tích lại) - Chỉ học sinh và phụ huynh
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> AnalyzeVideo(string lessonId, string mediaId, CancellationToken ct)
        {
            var userId = User.RequireUserId();
            await CheckAccessPermissionAsync(userId, lessonId);

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
                // Sử dụng IServiceScopeFactory để tạo scope mới cho background task
                // Tạo CancellationToken mới với timeout 30 phút để không bị cancel khi HTTP request kết thúc
                _logger.LogInformation($"Bắt đầu phân tích video: MediaId={mediaId}, LessonId={lessonId}, VideoUrl={media.FileUrl}");
                
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // Timeout 30 phút cho video lớn
                var backgroundCt = cts.Token;
                
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var videoAnalysisService = scope.ServiceProvider.GetRequiredService<IVideoAnalysisService>();
                    var uow = scope.ServiceProvider.GetRequiredService<DataLayer.Repositories.Abstraction.IUnitOfWork>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<VideoAnalysisController>>();
                    
                    try
                    {
                        logger.LogInformation($"Background task: Bắt đầu phân tích video {mediaId}");
                        var result = await videoAnalysisService.AnalyzeVideoAsync(mediaId, lessonId, media.FileUrl, backgroundCt);
                        logger.LogInformation($"Background task: Phân tích video {mediaId} thành công. Status={result.Status}");
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogWarning($"Background task: Phân tích video {mediaId} bị timeout sau 30 phút");
                        try
                        {
                            var failedAnalysis = await uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
                            if (failedAnalysis != null)
                            {
                                failedAnalysis.Status = VideoAnalysisStatus.Failed;
                                failedAnalysis.ErrorMessage = "Phân tích video bị timeout sau 30 phút. Video có thể quá lớn hoặc mạng chậm. Vui lòng thử lại với video nhỏ hơn.";
                                failedAnalysis.UpdatedAt = DateTime.Now;
                                await uow.VideoAnalyses.UpdateAsync(failedAnalysis);
                                await uow.SaveChangesAsync();
                            }
                        }
                        catch (Exception dbEx)
                        {
                            logger.LogError(dbEx, $"Background task: Lỗi khi cập nhật timeout status: {dbEx.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error chi tiết
                        logger.LogError(ex, $"Background task: Lỗi khi phân tích video {mediaId}: {ex.Message}");
                        logger.LogError($"Stack trace: {ex.StackTrace}");
                        
                        try
                        {
                            var failedAnalysis = await uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
                            if (failedAnalysis != null)
                            {
                                failedAnalysis.Status = VideoAnalysisStatus.Failed;
                                failedAnalysis.ErrorMessage = $"{ex.Message}\n\nStack: {ex.StackTrace}";
                                failedAnalysis.UpdatedAt = DateTime.Now;
                                await uow.VideoAnalyses.UpdateAsync(failedAnalysis);
                                await uow.SaveChangesAsync();
                                logger.LogInformation($"Background task: Đã cập nhật status Failed cho video {mediaId}");
                            }
                        }
                        catch (Exception dbEx)
                        {
                            logger.LogError(dbEx, $"Background task: Lỗi khi cập nhật failed status: {dbEx.Message}");
                        }
                    }
                }, backgroundCt);

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
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"AnalyzeVideo - Unauthorized: {ex.Message}");
                return Unauthorized(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AnalyzeVideo - Error: {ex.Message}");
                return BadRequest(ApiResponse<object>.Fail($"Lỗi khi phân tích video: {ex.Message}"));
            }
        }

        /// <summary>
        /// Hỏi câu hỏi về video (chỉ học sinh và phụ huynh)
        /// </summary>
        [HttpPost("ask")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> AskQuestion(
            string lessonId,
            string mediaId,
            [FromBody] VideoQuestionRequestDto request,
            CancellationToken ct)
        {
            try
            {
                // Kiểm tra quyền truy cập
                var userId = User.RequireUserId();
                await CheckAccessPermissionAsync(userId, lessonId);

                if (string.IsNullOrWhiteSpace(request.Question))
                    return BadRequest(ApiResponse<object>.Fail("Câu hỏi không được để trống."));

                _logger.LogInformation($"AskQuestion - MediaId: {mediaId}, Question: {request.Question}");
                
                // Tạo CancellationToken mới với timeout 2 phút để tránh bị cancel khi HTTP request kết thúc
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var questionCt = cts.Token;
                
                var response = await _videoAnalysisService.AnswerQuestionAsync(mediaId, request, questionCt);
                
                // Kiểm tra response
                if (response == null)
                {
                    _logger.LogWarning("AskQuestion - Response is null");
                    return BadRequest(ApiResponse<object>.Fail("Không thể tạo response."));
                }
                
                if (string.IsNullOrEmpty(response.Answer))
                {
                    _logger.LogWarning($"AskQuestion - Answer is empty. Question: {request.Question}");
                    return BadRequest(ApiResponse<object>.Fail("Không thể trả lời câu hỏi này. Vui lòng thử lại hoặc đảm bảo video đã được phân tích hoàn tất."));
                }
                
                _logger.LogInformation($"AskQuestion - Success. Answer length: {response.Answer.Length}");
                return Ok(ApiResponse<object>.Ok(response, "Trả lời câu hỏi thành công"));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AskQuestion - Operation was canceled (timeout)");
                return BadRequest(ApiResponse<object>.Fail("Yêu cầu trả lời câu hỏi bị timeout. Vui lòng thử lại với câu hỏi ngắn gọn hơn."));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"AskQuestion - Unauthorized: {ex.Message}");
                return Unauthorized(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AskQuestion - Error: {ex.Message}");
                _logger.LogError($"AskQuestion - Stack trace: {ex.StackTrace}");
                return BadRequest(ApiResponse<object>.Fail($"Lỗi khi trả lời câu hỏi: {ex.Message}"));
            }
        }

        #region Helper Methods

        private async Task CheckAccessPermissionAsync(string userId, string lessonId)
        {
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);

            if (lesson == null || cls == null)
            {
                _logger.LogWarning($"CheckAccessPermission - Lesson or Class not found. LessonId: {lessonId}, UserId: {userId}");
                throw new UnauthorizedAccessException("Không tìm thấy bài học hoặc lớp học.");
            }

            _logger.LogInformation($"CheckAccessPermission - UserId: {userId}, LessonId: {lessonId}, ClassId: {cls.Id}");

            // Chỉ học sinh và phụ huynh được truy cập - không cần kiểm tra enroll
            var studentProfileId = await _uow.StudentProfiles.GetIdByUserIdAsync(userId);
            
            if (studentProfileId != null)
            {
                _logger.LogInformation($"CheckAccessPermission - User is Student. StudentProfileId: {studentProfileId}. Cho phép phân tích video.");
                // Student có thể phân tích video mà không cần enroll vào lớp
                return;
            }
            
            // Kiểm tra quyền Parent
            var parentProfile = await _uow.ParentProfiles.GetByUserIdAsync(userId);
            if (parentProfile != null)
            {
                _logger.LogInformation($"CheckAccessPermission - User is Parent. ParentProfileId: {parentProfile.Id}. Cho phép phân tích video.");
                // Parent có thể phân tích video mà không cần con enroll vào lớp
                return;
            }

            // Nếu không phải Student và không phải Parent
            throw new UnauthorizedAccessException("Chỉ học sinh và phụ huynh mới có quyền phân tích video bài giảng.");
        }

        #endregion
    }
}

