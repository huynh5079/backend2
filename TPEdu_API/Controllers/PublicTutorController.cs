using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Admin.Tutors;
using BusinessLayer.Service.Interface;
using DataLayer.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/tutor")]
    public class PublicTutorController : ControllerBase
    {
        private readonly IPublicTutorService _svc;
        public PublicTutorController(IPublicTutorService svc) => _svc = svc;

        // GET tpedu/v1/tutor?page=1
        // GET tpedu/v1/tutor?keyword=toán&subject=Toán&mode=Online&minRating=4.0&page=1
        [HttpGet]
        public async Task<IActionResult> GetApprovedTutors(
            [FromQuery] string? keyword = null,
            [FromQuery] string? subject = null,
            [FromQuery] string? educationLevel = null,
            [FromQuery] string? mode = null,
            [FromQuery] string? area = null,
            [FromQuery] string? gender = null,
            [FromQuery] double? minRating = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 6)
        {
            // Nếu có bất kỳ filter nào, dùng search/filter method
            bool hasFilter = !string.IsNullOrWhiteSpace(keyword) ||
                            !string.IsNullOrWhiteSpace(subject) ||
                            !string.IsNullOrWhiteSpace(educationLevel) ||
                            !string.IsNullOrWhiteSpace(mode) ||
                            !string.IsNullOrWhiteSpace(area) ||
                            !string.IsNullOrWhiteSpace(gender) ||
                            minRating.HasValue ||
                            minPrice.HasValue ||
                            maxPrice.HasValue;

            if (hasFilter)
            {
                ClassMode? modeEnum = null;
                if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse<ClassMode>(mode, true, out var parsedMode))
                {
                    modeEnum = parsedMode;
                }

                Gender? genderEnum = null;
                if (!string.IsNullOrWhiteSpace(gender) && Enum.TryParse<Gender>(gender, true, out var parsedGender))
                {
                    genderEnum = parsedGender;
                }

                var filter = new TutorSearchFilterDto
                {
                    Keyword = keyword,
                    Subject = subject,
                    EducationLevel = educationLevel,
                    Mode = modeEnum,
                    Area = area,
                    Gender = genderEnum,
                    MinRating = minRating,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Page = page,
                    PageSize = pageSize
                };

                var rs = await _svc.SearchAndFilterTutorsAsync(filter);
                if (!rs.Data.Any())
                    return NotFound(ApiResponse<object>.Fail("không tìm thấy"));

                var data = rs.Data.Select(x => new
                {
                    tutorId = x.TutorId,
                    username = x.Username,
                    email = x.Email,
                    teachingSubjects = x.TeachingSubjects,
                    teachingLevel = x.TeachingLevel,
                    createDate = x.CreateDate,
                    avatarUrl = x.AvatarUrl
                });

                return Ok(ApiResponse<object>.Ok(new { items = data, page = rs.PageNumber, size = rs.PageSize, total = rs.TotalCount }, "lấy danh sách thành công"));
            }
            else
            {
                // Không có filter, dùng method cũ
                var rs = await _svc.GetApprovedTutorsPagedAsync(page, pageSize);
                if (!rs.Data.Any())
                    return NotFound(ApiResponse<object>.Fail("không tìm thấy"));

                var data = rs.Data.Select(x => new
                {
                    tutorId = x.TutorId,
                    username = x.Username,
                    email = x.Email,
                    teachingSubjects = x.TeachingSubjects,
                    teachingLevel = x.TeachingLevel,
                    createDate = x.CreateDate,
                    avatarUrl = x.AvatarUrl
                });

                return Ok(ApiResponse<object>.Ok(new { items = data, page = rs.PageNumber, size = rs.PageSize, total = rs.TotalCount }, "lấy danh sách thành công"));
            }
        }

        // GET tpedu/v1/tutor/detail/:id
        [HttpGet("detail/{id}")]
        public async Task<IActionResult> GetTutorDetail(string id)
        {
            var detail = await _svc.GetApprovedTutorDetailAsync(id);
            if (detail == null)
                return NotFound(ApiResponse<object>.Fail("không tìm thấy"));

            return Ok(ApiResponse<object>.Ok(detail, "lấy danh sách thành công"));
        }
    }
}
