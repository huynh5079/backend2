using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.DTOs.Schedule.ClassAssign;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [Route("tpedu/v1/classes")]
    [ApiController]
    public class ClassController : ControllerBase
    {
        private readonly IClassService _classService;
        private readonly ITutorProfileService _tutorProfileService;
        private readonly IAssignService _assignService;

        public ClassController(
            IClassService classService,
            ITutorProfileService tutorProfileService,
            IAssignService assignService)
        {
            _classService = classService;
            _tutorProfileService = tutorProfileService;
            _assignService = assignService;
        }

        #region POST
        [HttpPost]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> CreateClass([FromBody] CreateClassDto createDto)
        {
            var userId = User.RequireUserId();
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(userId);

            if (string.IsNullOrEmpty(tutorProfileId))
            {
                // SỬA LỖI Ở ĐÂY: Dùng StatusCode() để set 403
                // và .Fail() chỉ với 1 tham số làm body.
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail("Người dùng chưa được cấp phép làm gia sư."));
            }

            var createdClass = await _classService.CreateRecurringClassScheduleAsync(tutorProfileId, createDto);

            return CreatedAtAction(
                nameof(GetClassById),
                new { id = createdClass.Id },
                ApiResponse<ClassDto>.Ok(createdClass));
        }
        #endregion

        #region GET
        [HttpGet("available")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableClasses(
            [FromQuery] string? keyword = null,
            [FromQuery] string? subject = null,
            [FromQuery] string? educationLevel = null,
            [FromQuery] string? mode = null,
            [FromQuery] string? area = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            // Nếu có bất kỳ filter nào, dùng search/filter method
            bool hasFilter = !string.IsNullOrWhiteSpace(keyword) ||
                            !string.IsNullOrWhiteSpace(subject) ||
                            !string.IsNullOrWhiteSpace(educationLevel) ||
                            !string.IsNullOrWhiteSpace(mode) ||
                            !string.IsNullOrWhiteSpace(area) ||
                            minPrice.HasValue ||
                            maxPrice.HasValue ||
                            !string.IsNullOrWhiteSpace(status);

            if (hasFilter)
            {
                ClassMode? modeEnum = null;
                if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse<ClassMode>(mode, true, out var parsedMode))
                {
                    modeEnum = parsedMode;
                }

                ClassStatus? statusEnum = null;
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ClassStatus>(status, true, out var parsedStatus))
                {
                    statusEnum = parsedStatus;
                }

                var filter = new ClassSearchFilterDto
                {
                    Keyword = keyword,
                    Subject = subject,
                    EducationLevel = educationLevel,
                    Mode = modeEnum,
                    Area = area,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Status = statusEnum,
                    Page = page,
                    PageSize = pageSize
                };

                var rs = await _classService.SearchAndFilterAvailableAsync(filter);
                return Ok(ApiResponse<object>.Ok(new { items = rs.Data, page = rs.PageNumber, size = rs.PageSize, total = rs.TotalCount }));
            }
            else
            {
                // Không có filter, dùng method cũ
                var classes = await _classService.GetAvailableClassesAsync();
                return Ok(ApiResponse<IEnumerable<ClassDto>>.Ok(classes));
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetClassById(string id)
        {
            var classDetail = await _classService.GetClassByIdAsync(id);
            return Ok(ApiResponse<ClassDto>.Ok(classDetail));
        }

        [HttpGet("my-classes")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetMyClasses()
        {
            var tutorUserId = User.RequireUserId();
            var classes = await _classService.GetMyClassesAsync(tutorUserId);
            return Ok(ApiResponse<IEnumerable<ClassDto>>.Ok(classes));
        }

        /// <summary>
        /// [Tutor] Get list of students enrolled in a class.
        /// </summary>
        [HttpGet("{classId}/students")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> GetStudentsInClass(string classId)
        {
            var tutorUserId = User.RequireUserId();
            var students = await _assignService.GetStudentsInClassAsync(tutorUserId, classId);
            return Ok(ApiResponse<List<StudentEnrollmentDto>>.Ok(students, "Lấy danh sách học sinh thành công."));
        }
        #endregion

        #region PUT
        [HttpPut("{id}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> UpdateClass(string id, [FromBody] UpdateClassDto dto)
        {
            var tutorUserId = User.RequireUserId();
            var updatedClass = await _classService.UpdateClassAsync(tutorUserId, id, dto);
            return Ok(ApiResponse<ClassDto>.Ok(updatedClass));
        }

        [HttpPut("{id}/schedules")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> UpdateClassSchedule(string id, [FromBody] UpdateClassScheduleDto dto)
        {
            var tutorUserId = User.RequireUserId();
            await _classService.UpdateClassScheduleAsync(tutorUserId, id, dto);
            return Ok(ApiResponse<object>.Ok(null, "Cập nhật lịch học thành công."));
        }
        #endregion

        #region DELETE
        [HttpDelete("{id}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> DeleteClass(string id)
        {
            var tutorUserId = User.RequireUserId();
            await _classService.DeleteClassAsync(tutorUserId, id);
            return Ok(ApiResponse<object>.Ok(null, "Xóa lớp học thành công."));
        }
        #endregion
    }
}