using BusinessLayer.DTOs.Admin.Parent;
using BusinessLayer.DTOs.API;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/parent/manage-children")]
    [Authorize(Roles = "Parent")]
    public class ParentChildrenController : ControllerBase
    {
        private readonly IParentChildrenService _svc;
        public ParentChildrenController(IParentChildrenService svc) => _svc = svc;

        // GET tpedu/v1/parent/manage-children?page=1
        [HttpGet]
        public async Task<IActionResult> GetMyChildren([FromQuery] int page = 1)
        {
            var parentId = User.RequireUserId(); // extension bạn đã có
            var rs = await _svc.GetMyChildrenPagedAsync(parentId, page, 5);
            return Ok(ApiResponse<object>.Ok(rs.Data, "lấy danh sách thành công"));
        }

        // GET tpedu/v1/parent/manage-children/detail/{studentId}
        [HttpGet("detail/{studentId}")]
        public async Task<IActionResult> GetChildDetail(string studentId)
        {
            var parentId = User.RequireUserId();
            var detail = await _svc.GetChildDetailAsync(parentId, studentId);
            if (detail == null) return NotFound(ApiResponse<object>.Fail("không tìm thấy"));
            return Ok(ApiResponse<object>.Ok(detail, "lấy danh sách thành công"));
        }

        // POST tpedu/v1/parent/manage-children/create-account  (tạo tài khoản con)
        [HttpPost("create-account")]
        public async Task<IActionResult> CreateChild([FromBody] CreateChildRequest req)
        {
            var parentId = User.RequireUserId();
            var (ok, msg, data) = await _svc.CreateChildAsync(parentId, req);
            if (!ok) return BadRequest(ApiResponse<object>.Fail(msg));
            return Ok(ApiResponse<object>.Ok(data, msg));
        }

        // POST tpedu/v1/parent/manage-children/link  (liên kết học sinh có sẵn)
        [HttpPost("link")]
        public async Task<IActionResult> LinkChild([FromBody] LinkExistingChildRequest req)
        {
            var parentId = User.RequireUserId();
            var (ok, msg) = await _svc.LinkExistingChildAsync(parentId, req);
            if (!ok) return BadRequest(ApiResponse<object>.Fail(msg));
            return Ok(ApiResponse<object>.Ok(null, msg));
        }

        // PUT tpedu/v1/parent/manage-children/update/{studentId}  (cập nhật info)
        [HttpPut("update/{studentId}")]
        public async Task<IActionResult> UpdateChild(string studentId, [FromBody] UpdateChildRequest req)
        {
            var parentId = User.RequireUserId();
            var (ok, msg, data) = await _svc.UpdateChildAsync(parentId, studentId, req);
            if (!ok) return BadRequest(ApiResponse<object>.Fail(msg));
            return Ok(ApiResponse<object>.Ok(data, msg));
        }

        // DELETE tpedu/v1/parent/manage-children/unlink-with-children/{studentId} (gỡ liên kết)
        [HttpDelete("unlink-with-children/{studentId}")]
        public async Task<IActionResult> UnlinkChild(string studentId)
        {
            var parentId = User.RequireUserId();
            var (ok, msg) = await _svc.UnlinkChildAsync(parentId, studentId);
            if (!ok) return BadRequest(ApiResponse<object>.Fail(msg));
            return Ok(ApiResponse<object>.Ok(null, msg));
        }
    }

}
