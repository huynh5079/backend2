using BusinessLayer.DTOs.Authentication.Login;
using BusinessLayer.DTOs.Authentication.Password;
using BusinessLayer.DTOs.Authentication.Register;

namespace BusinessLayer.Service.Interface;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto);
    Task RegisterStudentAsync(RegisterStudentRequest dto);
    Task RegisterParentAsync(RegisterParentRequest dto);
    Task RegisterTutorAsync(RegisterTutorRequest dto);
    Task<bool> IsEmailAvailableAsync(string email);
    Task<(bool ok, string message)> ChangePasswordAsync(string userId, ChangePasswordRequest req);
    // Forgot password (sau khi OTP reset đã verify)
    Task<(bool ok, string message)> ResetPasswordAsync(ForgotPasswordRequest req);
}