using BusinessLayer.DTOs.Authentication.Login;
using BusinessLayer.DTOs.Authentication.Password;
using BusinessLayer.DTOs.Authentication.Register;
using BusinessLayer.Helper;
using BusinessLayer.Service.Interface;
using BusinessLayer.Storage;
using BusinessLayer.Utils;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client.Extensions.Msal;

namespace BusinessLayer.Service;

public class AuthService : IAuthService
{
    private readonly TpeduContext _db;
    private readonly ITokenService _token;
    private readonly IUnitOfWork _uow;
    private readonly IOtpService _otp;
    private readonly IFileStorageService _storage;
    private readonly IMediaService _media;

    private const int StudentMinAge = 15;
    private const int TutorMinAge = 18;

    public AuthService(TpeduContext db, ITokenService token, IUnitOfWork uow, IOtpService otp, IFileStorageService storage, IMediaService media)
    {
        _db = db; 
        _token = token; 
        _uow = uow; 
        _otp = otp; 
        _storage = storage; 
        _media = media;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return null;

        var email = dto.Email.Trim();
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            return null;

        // ✅ Unban tự động nếu BannedUntil đã hết hạn
        if (await TryAutoUnbanIfExpiredAsync(user))
        {
            // user đã được cập nhật và SaveChanges bên trong helper
        }

        // ✅ chặn login nếu vẫn đang bị ban
        if (user.IsBanned || user.Status == AccountStatus.Banned)
            throw new UnauthorizedAccessException("tài khoản đã bị khóa, vui lòng liên hệ quản trị viên");

        if (!HashPasswordHelper.VerifyPassword(dto.Password, user.PasswordHash))
            return null;

        var token = _token.CreateToken(user);

        return new LoginResponseDto
        {
            Token = token,
            User = new LoggedInUserDto
            {
                Id = user.Id,
                Username = user.UserName ?? user.Email!,
                AvatarUrl = user.AvatarUrl,
                Role = user.RoleName
            }
        };
    }

    private async Task<bool> TryAutoUnbanIfExpiredAsync(User user)
    {
        var isCurrentlyBanned = user.IsBanned || user.Status == AccountStatus.Banned;
        if (!isCurrentlyBanned) 
            return false;

        if (!user.BannedUntil.HasValue) 
            return false;

        if (user.BannedUntil.Value > DateTime.Now) 
            return false;

        user.IsBanned = false;
        user.Status = AccountStatus.Active;
        user.BannedAt = null;
        user.BannedUntil = null;
        user.BannedReason = null;
        user.UpdatedAt = DateTime.Now;

        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task RegisterStudentAsync(RegisterStudentRequest dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (!await _otp.IsVerifiedAsync(email, OtpPurpose.Register))
            throw new InvalidOperationException("Vui lòng xác thực email trước khi đăng ký.");
        await EnsureEmailAvailable(email);

        AgeUtil.ValidateDob(dto.DateOfBirth);
        if (AgeUtil.IsMinor(dto.DateOfBirth, StudentMinAge))
            throw new InvalidOperationException("Học sinh chưa đủ tuổi đăng ký, vui lòng để phụ huynh đăng ký và tạo tài khoản cho con.");

        var role = await RequireRole(RoleEnum.Student);
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                UserName = dto.Username,
                Email = email,
                PasswordHash = HashPasswordHelper.HashPassword(dto.Password),
                RoleId = role.Id,
                RoleName = role.RoleName.ToString(),
                Status = AccountStatus.Active,
                AvatarUrl = AvatarHelper.ForStudent(),
                DateOfBirth = dto.DateOfBirth,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _uow.Users.CreateAsync(user);
            await _uow.SaveChangesAsync();

            var profile = new StudentProfile { UserId = user.Id };
            await _uow.StudentProfiles.CreateAsync(profile);
            await _uow.SaveChangesAsync();

            await tx.CommitAsync();
            await _otp.ConsumeVerifiedFlagAsync(email, OtpPurpose.Register);
        }
        catch { throw; }
    }

    public async Task RegisterParentAsync(RegisterParentRequest dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (!await _otp.IsVerifiedAsync(email, OtpPurpose.Register))
            throw new InvalidOperationException("Vui lòng xác thực email trước khi đăng ký.");
        await EnsureEmailAvailable(email);

        AgeUtil.ValidateDob(dto.DateOfBirth);

        var role = await RequireRole(RoleEnum.Parent);
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                UserName = dto.Username,
                Email = email,
                PasswordHash = HashPasswordHelper.HashPassword(dto.Password),
                RoleId = role.Id,
                RoleName = role.RoleName.ToString(),
                Status = AccountStatus.Active,
                AvatarUrl = AvatarHelper.ForParent(),
                DateOfBirth = dto.DateOfBirth,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _uow.Users.CreateAsync(user);
            await _uow.SaveChangesAsync();

            var profile = new ParentProfile { UserId = user.Id };
            await _uow.ParentProfiles.CreateAsync(profile);
            await _uow.SaveChangesAsync();

            await tx.CommitAsync();
            await _otp.ConsumeVerifiedFlagAsync(email, OtpPurpose.Register);
        }
        catch { throw; }
    }

    public async Task RegisterTutorAsync(RegisterTutorRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (!await _otp.IsVerifiedAsync(email, OtpPurpose.Register))
            throw new InvalidOperationException("Vui lòng xác thực email trước khi đăng ký.");
        await EnsureEmailAvailable(email);

        AgeUtil.ValidateDob(req.DateOfBirth);
        var age = AgeUtil.CalculateAge(req.DateOfBirth);
        if (!age.HasValue || age.Value < TutorMinAge)
            throw new InvalidOperationException("Gia sư phải đủ 18 tuổi để đăng ký.");

        var role = await RequireRole(RoleEnum.Tutor);
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                UserName = req.Username,
                Email = email,
                Gender = req.Gender,
                Phone = req.PhoneNumber,
                Address = req.Address,
                PasswordHash = HashPasswordHelper.HashPassword(req.Password),
                RoleId = role.Id,
                RoleName = role.RoleName.ToString(),
                Status = AccountStatus.PendingApproval,
                AvatarUrl = AvatarHelper.ForTutor(req.Gender),
                DateOfBirth = req.DateOfBirth,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _uow.Users.CreateAsync(user);
            await _uow.SaveChangesAsync();

            var tutor = new TutorProfile
            {
                UserId = user.Id,
                Bio = req.SelfDescription,
                ExperienceDetails = req.ExperienceDetails,
                Rating = null,
                ApprovedByAdmin = false,
                EducationLevel = req.EducationLevel,
                University = req.University,
                Major = req.Major,
                TeachingExperienceYears = req.TeachingExperienceYears,
                TeachingSubjects = req.TeachingSubjects != null ? string.Join(",", req.TeachingSubjects) : null,
                TeachingLevel = req.TeachingLevel != null ? string.Join(",", req.TeachingLevel) : null,
                SpecialSkills = req.SpecialSkills != null ? string.Join(",", req.SpecialSkills) : null
            };
            await _uow.TutorProfiles.CreateAsync(tutor);
            await _uow.SaveChangesAsync();

            if (req.IdentityDocuments is { Count: > 0 })
            {
                var ups = await _storage.UploadManyAsync(req.IdentityDocuments, UploadContext.IdentityDocument, user.Id);
                await _media.SaveTutorIdentityDocsAsync(user.Id, ups);
            }
            if (req.CertificateFiles is { Count: > 0 })
            {
                var ups = await _storage.UploadManyAsync(req.CertificateFiles, UploadContext.Certificate, user.Id);
                await _media.SaveTutorCertificatesAsync(user.Id, tutor.Id, ups);
            }

            await _uow.SaveChangesAsync();
            await tx.CommitAsync();
            await _otp.ConsumeVerifiedFlagAsync(email, OtpPurpose.Register);
        }
        catch { throw; }
    }

    public async Task<(bool ok, string message)> ChangePasswordAsync(string userId, ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "tài khoản không hợp lệ");

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return (false, "mật khẩu mới phải có độ dài ít nhất 8 ký tự");

        // Lấy user
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null)
            return (false, "tài khoản không tồn tại");

        // Nếu tài khoản đăng ký Google hoặc chưa có mật khẩu
        if (string.IsNullOrEmpty(user.PasswordHash))
            return (false, "tài khoản không hỗ trợ đổi mật khẩu bằng phương thức này");

        // Check old password
        if (!HashPasswordHelper.VerifyPassword(req.OldPassword, user.PasswordHash))
            return (false, "mật khẩu cũ không đúng");

        // Cập nhật password
        user.PasswordHash = HashPasswordHelper.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.Now;

        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        return (true, "đổi mật khẩu thành công");
    }

    // Forgot password (sau khi OTP reset đã verify)
    public async Task<(bool ok, string message)> ResetPasswordAsync(ForgotPasswordRequest req)
    {
        var email = req.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return (false, "email không hợp lệ");
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return (false, "mật khẩu mới phải có độ dài ít nhất 8 ký tự");

        var user = await _uow.Users.FindByEmailAsync(email);
        if (user == null) return (false, "email không tồn tại");

        // bắt buộc đã verify OTP cho mục đích ResetPassword
        if (!await _otp.IsVerifiedAsync(email, OtpPurpose.ResetPassword))
            return (false, "vui lòng xác thực OTP trước khi đặt lại mật khẩu");

        user.PasswordHash = HashPasswordHelper.HashPassword(req.NewPassword);
        user.UpdatedAt = DateTime.Now;
        await _uow.Users.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        await _otp.ConsumeVerifiedFlagAsync(email, OtpPurpose.ResetPassword);
        return (true, "đổi mật khẩu thành công");
    }

    // ==== helpers ====
    private async Task EnsureEmailAvailable(string email)
    {
        if (await _uow.Users.ExistsByEmailAsync(email))
            throw new InvalidOperationException("Email đã tồn tại");
    }

    private async Task<Role> RequireRole(RoleEnum roleEnum)
    {
        var role = await _uow.Roles.GetByEnumAsync(roleEnum);
        if (role == null) throw new InvalidOperationException($"Role '{roleEnum}' chưa được seed.");
        return role;
    }

    public async Task<bool> IsEmailAvailableAsync(string email)
    {
        return !await _db.Users.AnyAsync(u => u.Email == email);
    }
}