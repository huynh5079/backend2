using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [Route("tpedu/v1/quiz")]
    [ApiController]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;

        public QuizController(IQuizService quizService)
        {
            _quizService = quizService;
        }

        // ===== TUTOR ENDPOINTS =====

        /// <summary>
        /// Tutor uploads a quiz file (.txt or .docx) for a lesson
        /// </summary>
        [HttpPost("upload")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> UploadQuiz([FromForm] UploadQuizFileDto dto, CancellationToken ct)
        {
            try
            {
                var tutorUserId = User.RequireUserId();
                var quizId = await _quizService.CreateQuizFromFileAsync(tutorUserId, dto, ct);
                return Ok(ApiResponse<object>.Ok(new { quizId }, "Tạo quiz thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        /// <summary>
        /// Tutor deletes a quiz
        /// </summary>
        [HttpDelete("{quizId}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> DeleteQuiz(string quizId)
        {
            try
            {
                var tutorUserId = User.RequireUserId();
                var result = await _quizService.DeleteQuizAsync(tutorUserId, quizId);
                
                if (!result)
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy quiz"));
                
                return Ok(ApiResponse<object>.Ok(null, "Xóa quiz thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        /// <summary>
        /// Tutor gets quiz details by ID (includes correct answers)
        /// </summary>
        [HttpGet("{quizId}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetQuizById(string quizId)
        {
            try
            {
                var tutorUserId = User.RequireUserId();
                var quiz = await _quizService.GetQuizByIdAsync(tutorUserId, quizId);
                return Ok(ApiResponse<TutorQuizDto>.Ok(quiz, "Lấy thông tin quiz thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<TutorQuizDto>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<TutorQuizDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<TutorQuizDto>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get all quizzes for a lesson (Tutor or Student)
        /// </summary>
        [HttpGet("lesson/{lessonId}")]
        [Authorize]
        public async Task<IActionResult> GetQuizzesByLesson(string lessonId)
        {
            try
            {
                var userId = User.RequireUserId();
                var quizzes = await _quizService.GetQuizzesByLessonAsync(userId, lessonId);
                return Ok(ApiResponse<IEnumerable<QuizSummaryDto>>.Ok(quizzes, "Lấy danh sách quiz thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<IEnumerable<QuizSummaryDto>>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<IEnumerable<QuizSummaryDto>>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<QuizSummaryDto>>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        /// <summary>
        /// Tutor updates a quiz question (can add/change image)
        /// </summary>
        [HttpPut("question/{questionId}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> UpdateQuestion(string questionId, [FromForm] UpdateQuizQuestionDto dto)
        {
            try
            {
                var tutorUserId = User.RequireUserId();
                var result = await _quizService.UpdateQuizQuestionAsync(tutorUserId, questionId, dto);
                
                if (!result)
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy câu hỏi"));
                
                return Ok(ApiResponse<object>.Ok(null, "Cập nhật câu hỏi thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        // ===== STUDENT ENDPOINTS =====

        /// <summary>
        /// Student starts a quiz (gets questions without answers)
        /// </summary>
        [HttpPost("{quizId}/start")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StartQuiz(string quizId)
        {
            try
            {
                var studentUserId = User.RequireUserId();
                var quiz = await _quizService.StartQuizAsync(studentUserId, quizId);
                return Ok(ApiResponse<StudentQuizDto>.Ok(quiz, "Tải quiz thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<StudentQuizDto>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<StudentQuizDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<StudentQuizDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<StudentQuizDto>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        /// <summary>
        /// Student submits quiz answers and gets results
        /// </summary>
        [HttpPost("submit")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitQuiz([FromBody] SubmitQuizDto dto)
        {
            try
            {
                var studentUserId = User.RequireUserId();
                var result = await _quizService.SubmitQuizAsync(studentUserId, dto);
                return Ok(ApiResponse<QuizResultDto>.Ok(result, "Nộp bài thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<QuizResultDto>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<QuizResultDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<QuizResultDto>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        /// <summary>
        /// Student views their attempt history for a quiz
        /// </summary>
        [HttpGet("{quizId}/attempts")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyAttempts(string quizId)
        {
            try
            {
                var studentUserId = User.RequireUserId();
                var attempts = await _quizService.GetMyAttemptsAsync(studentUserId, quizId);
                return Ok(ApiResponse<IEnumerable<QuizResultDto>>.Ok(attempts, "Lấy lịch sử làm bài thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<IEnumerable<QuizResultDto>>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<QuizResultDto>>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }

        // ===== PARENT ENDPOINTS =====

        /// <summary>
        /// Parent xem lịch sử làm quiz của con
        /// </summary>
        [HttpGet("parent/{studentProfileId}/quiz/{quizId}/attempts")]
        [Authorize(Roles = "Parent")]
        public async Task<IActionResult> GetStudentAttemptsForParent(string studentProfileId, string quizId)
        {
            try
            {
                var parentUserId = User.RequireUserId();
                var attempts = await _quizService.GetStudentAttemptsForParentAsync(parentUserId, studentProfileId, quizId);
                return Ok(ApiResponse<IEnumerable<QuizResultDto>>.Ok(attempts, "Lấy kết quả quiz của học sinh thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<IEnumerable<QuizResultDto>>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<IEnumerable<QuizResultDto>>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<QuizResultDto>>.Fail($"Lỗi hệ thống: {ex.Message}"));
            }
        }
    }
}
