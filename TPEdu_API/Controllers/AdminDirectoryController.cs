using BusinessLayer.DTOs.API;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminDirectoryController : ControllerBase
    {
        private readonly IAdminDirectoryService _svc;
        public AdminDirectoryController(IAdminDirectoryService svc) => _svc = svc;

        // GET tpedu/v1/admin/student?page=1
        [HttpGet("student")]
        public async Task<IActionResult> GetStudents([FromQuery] int page = 1)
        {
            var rs = await _svc.GetStudentsPagedAsync(page, 5);
            var data = rs.Data.Select(x => new
            {
                studentId = x.StudentId,
                username = x.Username,
                email = x.Email,
                isBanned = x.IsBanned,
                createDate = x.CreateDate
            });
            return Ok(ApiResponse<object>.Ok(data, "lấy danh sách thành công"));
        }

        // GET tpedu/v1/admin/student/detail/:id
        [HttpGet("student/detail/{id}")]
        public async Task<IActionResult> GetStudentDetail(string id)
        {
            var detail = await _svc.GetStudentDetailAsync(id);
            if (detail == null)
                return NotFound(ApiResponse<object>.Fail("bạn không có"));

            return Ok(ApiResponse<object>.Ok(detail, "lấy danh sách thành công"));
        }

        // GET tpedu/v1/admin/parent?page=1
        [HttpGet("parent")]
        public async Task<IActionResult> GetParents([FromQuery] int page = 1)
        {
            var rs = await _svc.GetParentsPagedAsync(page, 5);
            var data = rs.Data.Select(x => new
            {
                parentId = x.ParentId,
                username = x.Username,
                email = x.Email,
                isBanned = x.IsBanned,
                createDate = x.CreateDate
            });
            return Ok(ApiResponse<object>.Ok(data, "lấy danh sách thành công"));
        }

        // GET tpedu/v1/admin/parent/detail/:id
        [HttpGet("parent/detail/{id}")]
        public async Task<IActionResult> GetParentDetail(string id)
        {
            var detail = await _svc.GetParentDetailAsync(id);
            if (detail == null)
                return NotFound(ApiResponse<object>.Fail("bạn không có"));

            return Ok(ApiResponse<object>.Ok(detail, "lấy danh sách thành công"));
        }
    }

}
