using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.Lesson;
using BusinessLayer.Service.Interface.IScheduleService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        [Authorize(Roles = "Student,Parent")] // only Student and Parent can access
        public async Task<IActionResult> GetLessonsByClass(string classId)
        {
            var result = await _lessonService.GetLessonsByClassIdAsync(classId);
            return Ok(ApiResponse<IEnumerable<ClassLessonDto>>.Ok(result, "Lấy danh sách buổi học thành công"));
        }

        /// <summary>
        /// Lấy thông tin chi tiết của một buổi học
        /// </summary>
        /// <param name="id">ID của buổi học (LessonId)</param>
        [HttpGet("{id}")]
        [Authorize(Roles = "Student,Parent")] // only Student and Parent can access
        public async Task<IActionResult> GetLessonDetail(string id)
        {
            var result = await _lessonService.GetLessonDetailAsync(id);

            if (result == null)
            {

                return NotFound(ApiResponse<object>.Fail($"Không tìm thấy buổi học với ID '{id}'."));
            }

            return Ok(ApiResponse<LessonDetailDto>.Ok(result, "Lấy chi tiết buổi học thành công"));
        }
    }
}