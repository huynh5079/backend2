using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.LessonMaterials;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/lessons/{lessonId}/materials")]
    public class LessonMaterialsController : ControllerBase
    {
        private readonly ILessonMaterialService _svc;
        public LessonMaterialsController(ILessonMaterialService svc) => _svc = svc;

        // 1) List (Tutor/Student/Parent*) — Parent có thể bạn xử lý ủy quyền ở middleware hoặc Service riêng
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> List(string lessonId)
        {
            var uid = User.RequireUserId();
            var items = await _svc.ListAsync(uid, lessonId);
            return Ok(ApiResponse<object>.Ok(items, "Lấy danh sách tài liệu thành công"));
        }

        // 2) Upload files (Tutor)
        [HttpPost("upload")]
        [Authorize(Roles = "Tutor")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(200_000_000)]
        public async Task<IActionResult> Upload(string lessonId, [FromForm] UploadLessonMaterialsRequest req, CancellationToken ct)
        {
            if (req.Files == null || req.Files.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn ít nhất 1 file."));

            try
            {
                var uid = User.RequireUserId();
                var items = await _svc.UploadAsync(uid, lessonId, req.Files, ct);
                return Ok(ApiResponse<object>.Ok(items, "Upload tài liệu thành công"));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("vi phạm quy định"))
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
        }

        // 3) Add links (Tutor)
        [HttpPost("links")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> AddLinks(string lessonId, [FromBody] AddLessonLinksRequest req)
        {
            if (req.Links == null || req.Links.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập ít nhất 1 link."));

            var uid = User.RequireUserId();
            var tuples = req.Links.Select((url, i) =>
            {
                string? title = (req.Titles != null && i < req.Titles.Count) ? req.Titles[i] : null;
                return (url, title);
            });
            var items = await _svc.AddLinksAsync(uid, lessonId, tuples);
            return Ok(ApiResponse<object>.Ok(items, "Thêm link thành công"));
        }

        // 4) Delete (Tutor)
        [HttpDelete("{mediaId}")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> Delete(string lessonId, string mediaId, CancellationToken ct)
        {
            var uid = User.RequireUserId();
            var ok = await _svc.DeleteAsync(uid, lessonId, mediaId, ct);
            return ok
                ? Ok(ApiResponse<object>.Ok(new { }, "Xoá tài liệu thành công"))
                : NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu để xoá"));
        }
    }
}
