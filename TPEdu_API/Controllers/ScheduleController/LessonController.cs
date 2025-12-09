using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.Lesson;
using BusinessLayer.Helper;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TPEdu_API.Controllers.ScheduleController
{
    [Route("tpedu/v1/lessons")]
    [ApiController]
    [Authorize]
    public class LessonController : ControllerBase
    {
        private readonly ILessonService _lessonService;

        public LessonController(ILessonService lessonService)
        {
            _lessonService = lessonService;
        }

        /// <summary>
        /// Lấy danh sách các buổi học của một lớp cụ thể
        /// </summary>
        /// <param name="classId">ID của lớp học</param>
        [HttpGet("class/{classId}")]
        [Authorize(Roles = "Student,Parent,Tutor")] // only Student and Parent can access
        public async Task<IActionResult> GetLessonsByClass(string classId)
        {
            var result = await _lessonService.GetLessonsByClassIdAsync(classId);
            return Ok(ApiResponse<IEnumerable<ClassLessonDto>>.Ok(result, "Lấy danh sách buổi học thành công"));
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một buổi học cho hs/ph
        /// </summary>
        /// <param name="id">ID của buổi học (LessonId)</param>
        [HttpGet("{id}")]
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> GetLessonDetail(string id)
        {
            var result = await _lessonService.GetLessonDetailAsync(id);

            if (result == null)
            {

                return NotFound(ApiResponse<object>.Fail($"Không tìm thấy buổi học với ID '{id}'."));
            }

            return Ok(ApiResponse<LessonDetailDto>.Ok(result, "Lấy chi tiết buổi học thành công"));
        }

        /// <summary>
        /// Lấy chi tiết buổi học dành cho Gia sư (bao gồm danh sách học sinh để điểm danh)
        /// </summary>
        /// <param name="id">LessonId</param>
        [HttpGet("{id}/tutor-detail")]
        [Authorize]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetTutorLessonDetail(string id)
        {
            try
            {
                // User id from token
                var tutorUserId = User.GetUserId();

                var result = await _lessonService.GetTutorLessonDetailAsync(id, tutorUserId);

                if (result == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy buổi học."));
                }

                return Ok(ApiResponse<TutorLessonDetailDto>.Ok(result, "Lấy thông tin buổi học (Gia sư) thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                // return 403 Forbidden if the tutor is not authorized to access this lesson
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                // other exceptions return 500 Internal Server Error
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message));
            }
        }
    }
}