using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Class;
using BusinessLayer.Service;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TPEdu_API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ClassController : ControllerBase
{
    private readonly IClassService _classService;
    private readonly IGenericRepository<User> _userRepository;
    private readonly ITokenService _tokenService;

    public ClassController(IClassService classService, IGenericRepository<User> userRepository, ITokenService tokenService)
    {
        _classService = classService;
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Tạo lớp học mới
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassDto createClassDto)
    {
        try
        {
            var tutorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tutorId))
            {
                return Unauthorized(ApiResponse<ClassResponseDto>.Fail("Không thể xác định gia sư"));
            }

            var result = await _classService.CreateClassAsync(createClassDto, tutorId);
            
            if (result.Status == "Success")
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<ClassResponseDto>.Fail($"Lỗi server: {ex.Message}"));
        }
    }

    /// <summary>
    /// Lấy danh sách lớp học của gia sư
    /// </summary>
    [HttpGet("tutor")]
    public async Task<IActionResult> GetClassesByTutor()
    {
        try
        {
            var tutorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(tutorId))
            {
                return Unauthorized(ApiResponse<List<ClassResponseDto>>.Fail("Không thể xác định gia sư"));
            }

            var result = await _classService.GetClassesByTutorAsync(tutorId);
            
            if (result.Status == "Success")
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<List<ClassResponseDto>>.Fail($"Lỗi server: {ex.Message}"));
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết lớp học
    /// </summary>
    [HttpGet("{classId}")]
    public async Task<IActionResult> GetClassById(string classId)
    {
        try
        {
            var result = await _classService.GetClassByIdAsync(classId);
            
            if (result.Status == "Success")
            {
                return Ok(result);
            }
            
            return NotFound(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<ClassResponseDto>.Fail($"Lỗi server: {ex.Message}"));
        }
    }

    /// <summary>
    /// Test endpoint để lấy token cho tutor có sẵn
    /// </summary>
    [HttpPost("test-login")]
    [AllowAnonymous]
    public async Task<IActionResult> TestLogin()
    {
        try
        {
            // Sử dụng tutorId có sẵn trong database
            var tutorId = "58381945-6aaf-405b-813c-f2a9e874f2d7"; // Võ Lê Thi
            
            var user = await _userRepository.GetByIdAsync(tutorId);
            if (user == null)
            {
                return BadRequest(ApiResponse<object>.Fail("Không tìm thấy tutor"));
            }

            // Tạo token cho tutor này
            var token = _tokenService.CreateToken(user);
            
            return Ok(ApiResponse<object>.Ok(new { 
                token = token,
                user = new {
                    id = user.Id,
                    username = user.UserName,
                    email = user.Email,
                    role = user.RoleName
                }
            }, "Lấy token thành công"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail($"Lỗi server: {ex.Message}"));
        }
    }

    /// <summary>
    /// Lấy danh sách lớp học có sẵn (cho học sinh xem)
    /// </summary>
    [HttpGet("available")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableClasses()
    {
        try
        {
            var result = await _classService.GetAvailableClassesAsync();
            
            if (result.Status == "Success")
            {
                return Ok(result);
            }
            
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<List<ClassResponseDto>>.Fail($"Lỗi server: {ex.Message}"));
        }
    }
}
