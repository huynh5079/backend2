//using BusinessLayer.DTOs.API;
//using BusinessLayer.DTOs.ClassRequest;
//using BusinessLayer.Service.Interface;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//namespace TPEdu_API.Controllers;

//[ApiController]
//[Route("tpedu/v1/class-requests")]
//[AllowAnonymous]
//public class ClassRequestController : ControllerBase
//{
//    private readonly IClassRequestService _classRequestService;

//    public ClassRequestController(IClassRequestService classRequestService)
//    {
//        _classRequestService = classRequestService;
//    }

//    /// <summary>
//    /// Tạo mới class request
//    /// </summary>
//    [HttpPost]
//    public async Task<IActionResult> CreateClassRequest([FromBody] CreateClassRequestDto dto)
//    {
//        if (!ModelState.IsValid)
//        {
//            return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ"));
//        }

//        var result = await _classRequestService.CreateClassRequestAsync(dto);
//        if (result == null)
//        {
//            return BadRequest(ApiResponse<object>.Fail("Không thể tạo class request"));
//        }

//        return Ok(ApiResponse<ClassRequestResponseDto>.Ok(result, "Tạo class request thành công"));
//    }

//    /// <summary>
//    /// Lấy tất cả class requests
//    /// </summary>
//    [HttpGet]
//    public async Task<IActionResult> GetAllClassRequests()
//    {
//        var result = await _classRequestService.GetAllClassRequestsAsync();
//        return Ok(ApiResponse<IEnumerable<ClassRequestResponseDto>>.Ok(result, "Lấy danh sách class requests thành công"));
//    }

//    /// <summary>
//    /// Lấy class request theo ID
//    /// </summary>
//    [HttpGet("{id}")]
//    public async Task<IActionResult> GetClassRequestById(string id)
//    {
//        var result = await _classRequestService.GetClassRequestByIdAsync(id);
//        if (result == null)
//        {
//            return NotFound(ApiResponse<object>.Fail("Không tìm thấy class request"));
//        }

//        return Ok(ApiResponse<ClassRequestResponseDto>.Ok(result, "Lấy class request thành công"));
//    }

//    /// <summary>
//    /// Lấy class requests theo Student ID
//    /// </summary>
//    [HttpGet("student/{studentId}")]
//    public async Task<IActionResult> GetClassRequestsByStudentId(string studentId)
//    {
//        var result = await _classRequestService.GetClassRequestsByStudentIdAsync(studentId);
//        return Ok(ApiResponse<IEnumerable<ClassRequestResponseDto>>.Ok(result, "Lấy danh sách class requests của student thành công"));
//    }

//    /// <summary>
//    /// Lấy class requests theo Tutor ID
//    /// </summary>
//    [HttpGet("tutor/{tutorId}")]
//    public async Task<IActionResult> GetClassRequestsByTutorId(string tutorId)
//    {
//        var result = await _classRequestService.GetClassRequestsByTutorIdAsync(tutorId);
//        return Ok(ApiResponse<IEnumerable<ClassRequestResponseDto>>.Ok(result, "Lấy danh sách class requests của tutor thành công"));
//    }

//    /// <summary>
//    /// Lấy danh sách class requests với pagination và filtering
//    /// </summary>
//    [HttpGet("list")]
//    public async Task<IActionResult> GetClassRequestsList(
//        [FromQuery] int page = 1, 
//        [FromQuery] int pageSize = 10, 
//        [FromQuery] string? status = null, 
//        [FromQuery] string? studentId = null, 
//        [FromQuery] string? tutorId = null,
//        [FromQuery] string? subjectId = null,
//        [FromQuery] string? educationLevelId = null,
//        [FromQuery] string? mode = null,
//        [FromQuery] string? scheduleContains = null,
//        [FromQuery] string? locationContains = null)
//    {
//        if (page < 1) page = 1;
//        if (pageSize < 1 || pageSize > 100) pageSize = 10;

//        var result = await _classRequestService.GetClassRequestsWithPaginationAsync(
//            page, pageSize, status, studentId, tutorId, subjectId, educationLevelId, mode, scheduleContains, locationContains);
        
//        var response = new
//        {
//            Data = result.Data,
//            TotalCount = result.TotalCount,
//            Page = page,
//            PageSize = pageSize,
//            TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize),
//            HasNextPage = page < (int)Math.Ceiling((double)result.TotalCount / pageSize),
//            HasPreviousPage = page > 1
//        };

//        return Ok(ApiResponse<object>.Ok(response, "Lấy danh sách class requests thành công"));
//    }

//    /// <summary>
//    /// Cập nhật class request
//    /// </summary>
//    [HttpPut("{id}")]
//    public async Task<IActionResult> UpdateClassRequest(string id, [FromBody] UpdateClassRequestDto dto)
//    {
//        if (!ModelState.IsValid)
//        {
//            return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ"));
//        }

//        dto.Id = id; // Set ID từ URL
//        var result = await _classRequestService.UpdateClassRequestAsync(dto);
//        if (result == null)
//        {
//            return NotFound(ApiResponse<object>.Fail("Không tìm thấy class request để cập nhật"));
//        }

//        return Ok(ApiResponse<ClassRequestResponseDto>.Ok(result, "Cập nhật class request thành công"));
//    }

//    /// <summary>
//    /// Cập nhật trạng thái class request
//    /// </summary>
//    [HttpPatch("{id}/status")]
//    public async Task<IActionResult> UpdateClassRequestStatus(string id, [FromBody] UpdateStatusDto dto)
//    {
//        if (string.IsNullOrEmpty(dto.Status))
//        {
//            return BadRequest(ApiResponse<object>.Fail("Status không được để trống"));
//        }

//        var result = await _classRequestService.UpdateClassRequestStatusAsync(id, dto.Status);
//        if (!result)
//        {
//            return NotFound(ApiResponse<object>.Fail("Không tìm thấy class request để cập nhật"));
//        }

//        return Ok(ApiResponse<object>.Ok(new { }, "Cập nhật trạng thái thành công"));
//    }

//    /// <summary>
//    /// Xóa class request (soft delete)
//    /// </summary>
//    [HttpDelete("{id}")]
//    public async Task<IActionResult> DeleteClassRequest(string id)
//    {
//        var result = await _classRequestService.DeleteClassRequestAsync(id);
//        if (!result)
//        {
//            return NotFound(ApiResponse<object>.Fail("Không tìm thấy class request để xóa"));
//        }

//        return Ok(ApiResponse<object>.Ok(new { }, "Xóa class request thành công"));
//    }

//    /// <summary>
//    /// Hủy class request
//    /// </summary>
//    [HttpPatch("{id}/cancel")]
//    public async Task<IActionResult> CancelClassRequest(string id)
//    {
//        // Validate cancel permission
//        var validationResult = await _classRequestService.ValidateCancelPermissionAsync(id);
//        if (!validationResult.Success)
//        {
//            return BadRequest(ApiResponse<object>.Fail(validationResult.Message));
//        }

//        var result = await _classRequestService.CancelClassRequestAsync(id);
//        if (!result)
//        {
//            return BadRequest(ApiResponse<object>.Fail("Không thể hủy class request. Có thể class request không tồn tại hoặc đã được hủy trước đó"));
//        }

//        return Ok(ApiResponse<object>.Ok(new { }, "Hủy class request thành công"));
//    }

//    /// <summary>
//    /// Tự động expire các request đã hết hạn (Background job)
//    /// </summary>
//    [HttpPost("expire")]
//    public async Task<IActionResult> ExpireClassRequests()
//    {
//        var expiredCount = await _classRequestService.ExpireClassRequestsAsync();
//        return Ok(ApiResponse<object>.Ok(new { ExpiredCount = expiredCount }, $"Đã expire {expiredCount} class request"));
//    }


//}
