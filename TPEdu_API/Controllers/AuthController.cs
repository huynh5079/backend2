using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Authentication.Login;
using BusinessLayer.DTOs.Authentication.Password;
using BusinessLayer.DTOs.Authentication.Register;
using BusinessLayer.DTOs.Email;
using BusinessLayer.DTOs.Otp;
using BusinessLayer.Helper;
using BusinessLayer.Service.Interface;
using DataLayer.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers;

[ApiController]
[Route("tpedu/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IOtpService _otp;

    public AuthController(IAuthService auth, IOtpService otp)
    {
        _auth = auth; 
        _otp = otp;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("dữ liệu đăng nhập không hợp lệ"));

        var res = await _auth.LoginAsync(dto);
        if (res == null)
            return Unauthorized(ApiResponse<object>.Fail("thông tin không chính xác"));

        return Ok(ApiResponse<object>.Ok(
            new { token = res.Token, user = res.User },
            "đăng nhập thành công"
        ));
    }

    // POST /tpedu/v1/auth/verify_email { email }
    [HttpPost("verify_email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] EmailRequest body)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("email không hợp lệ"));

        var email = body.Email.Trim();
        // nếu đã tồn tại => Conflict 409
        if (!await _auth.IsEmailAvailableAsync(email))
            return Conflict(ApiResponse<object>.Fail("email đã tồn tại"));

        await _otp.SendOtpAsync(email, OtpPurpose.Register);
        return Ok(ApiResponse<object>.Ok(new { }, "gửi mã otp thành công"));
    }

    // POST /tpedu/v1/auth/verify_otp { email, code }
    [HttpPost("verify_otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("thiếu email hoặc mã otp"));

        var ok = await _otp.VerifyOtpAsync(req.Email.Trim(), req.Code.Trim(), OtpPurpose.Register);
        if (!ok) 
            return BadRequest(ApiResponse<object>.Fail("mã otp không đúng hoặc đã hết hạn")); // 400

        return Ok(ApiResponse<object>.Ok(new { }, "xác thực mã otp thành công"));
    }

    [HttpPost("register/student")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterStudent([FromBody] RegisterStudentRequest req)
    {
        try 
        { 
            await _auth.RegisterStudentAsync(req); 
            return Ok(ApiResponse<object>.Ok(new { }, "đăng ký thành công")); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(ApiResponse<object>.Fail(ex.Message)); 
        }
    }

    [HttpPost("register/parent")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterParent([FromBody] RegisterParentRequest req)
    {
        try 
        { 
            await _auth.RegisterParentAsync(req); 
            return Ok(ApiResponse<object>.Ok(new { }, "đăng ký thành công")); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(ApiResponse<object>.Fail(ex.Message)); 
        }
    }

    [HttpPost("register/tutor")]
    [AllowAnonymous, Consumes("multipart/form-data")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> RegisterTutor([FromForm] RegisterTutorRequest req)
    {
        try 
        { 
            await _auth.RegisterTutorAsync(req); 
            return Ok(ApiResponse<object>.Ok(new { }, "đăng ký thành công")); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(ApiResponse<object>.Fail(ex.Message)); 
        }
    }

    [HttpPost("change_password")]
    [Authorize] // cần JWT
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = User.GetUserId();
        var (ok, message) = await _auth.ChangePasswordAsync(userId!, req);

        if (!ok)
        {
            if (message.Contains("ít nhất 8", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<object>.Fail(message));     // 400
            if (message.Contains("mật khẩu cũ không đúng", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(ApiResponse<object>.Fail(message));   // 401

            return BadRequest(ApiResponse<object>.Fail(message));         // fallback 400
        }

        return Ok(ApiResponse<object>.Ok(new { }, message));
    }

    // Gửi OTP reset
    [HttpPost("forgot_password/send_otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordSendOtp([FromBody] EmailRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("email không hợp lệ"));

        var email = req.Email.Trim();
        var exists = !await _auth.IsEmailAvailableAsync(email); // available=false => exists
        if (!exists)
            return NotFound(ApiResponse<object>.Fail("email không tồn tại")); // 404

        await _otp.SendOtpAsync(email, OtpPurpose.ResetPassword);
        return Ok(ApiResponse<object>.Ok(new { }, "đã gửi mã otp đặt lại mật khẩu"));
    }

    // Xác thực OTP reset
    [HttpPost("forgot_password/verify_otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordVerifyOtp([FromBody] VerifyOtpRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("thiếu email hoặc mã otp"));

        var ok = await _otp.VerifyOtpAsync(req.Email.Trim(), req.Code.Trim(), OtpPurpose.ResetPassword);
        if (!ok) return BadRequest(ApiResponse<object>.Fail("mã otp không đúng hoặc đã hết hạn"));

        return Ok(ApiResponse<object>.Ok(new { }, "xác thực otp thành công"));
    }

    // Đặt lại mật khẩu (yêu cầu đã verify OTP)
    [HttpPost("forgot_password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var (ok, msg) = await _auth.ResetPasswordAsync(req);
        if (!ok)
        {
            if (msg.Contains("ít nhất 8", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<object>.Fail(msg));         // 400
            if (msg.Contains("email không tồn tại", StringComparison.OrdinalIgnoreCase))
                return NotFound(ApiResponse<object>.Fail(msg));          // 404

            return BadRequest(ApiResponse<object>.Fail(msg));            // fallback 400
        }

        return Ok(ApiResponse<object>.Ok(new { }, msg));
    }
}