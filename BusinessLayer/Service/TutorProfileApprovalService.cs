using BusinessLayer.DTOs.Admin.TutorProfileApproval;
using BusinessLayer.DTOs.Media;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BusinessLayer.DTOs.Admin.Directory.AdminListDtos;

namespace BusinessLayer.Service
{
    public class TutorProfileApprovalService : ITutorProfileApprovalService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMediaRepository _mediaRepo;
        private readonly INotificationService _notificationService;

        public TutorProfileApprovalService(
            IUnitOfWork uow,
            IMediaRepository mediaRepo,
            INotificationService notificationService)
        {
            _uow = uow;
            _mediaRepo = mediaRepo;
            _notificationService = notificationService;
        }

        public async Task<IReadOnlyList<TutorReviewItemDto>> GetPendingAsync()
        {
            var rows = await _uow.TutorProfiles.GetPendingTutorsAsync();
            return rows.Select(x => new TutorReviewItemDto
            {
                UserId = x.user.Id,
                TutorProfileId = x.profile.Id,
                Email = x.user.Email!,
                UserName = x.user.UserName,
                AvatarUrl = x.user.AvatarUrl,
                University = x.profile.University,
                Major = x.profile.Major,
                TeachingExperienceYears = x.profile.TeachingExperienceYears,
                Status = x.user.Status.ToString()
            }).ToList();
        }

        public async Task<TutorReviewDetailDto?> GetDetailAsync(string userId)
        {
            var u = await _uow.Users.FindWithRoleByIdAsync(userId);
            if (u == null || u.RoleName != "Tutor") return null;

            var t = await _uow.TutorProfiles.GetByUserIdAsync(userId);
            if (t == null) return null;

            var idDocs = await _mediaRepo.GetByOwnerAndContextAsync(userId, UploadContext.IdentityDocument);
            var certs = await _mediaRepo.GetCertificatesByTutorProfileAsync(t.Id);

            return new TutorReviewDetailDto
            {
                UserId = u.Id,
                TutorProfileId = t.Id,
                Email = u.Email!,
                UserName = u.UserName,
                AvatarUrl = u.AvatarUrl,
                Phone = u.Phone,
                Address = u.Address,
                Gender = u.Gender?.ToString().ToLowerInvariant(),
                DateOfBirth = u.DateOfBirth,
                Bio = t.Bio,
                ExperienceDetails = t.ExperienceDetails,
                University = t.University,
                Major = t.Major,
                EducationLevel = t.EducationLevel,
                TeachingExperienceYears = t.TeachingExperienceYears,
                TeachingSubjects = t.TeachingSubjects,
                TeachingLevel = t.TeachingLevel,
                Status = u.Status.ToString(),
                ReviewStatus = t.ReviewStatus.ToString(),
                RejectReason = t.RejectReason,
                ProvideNote = t.ProvideNote,

                IdentityDocuments = idDocs.Select(m => new MediaItemDto
                {
                    Id = m.Id,
                    Url = m.FileUrl,
                    FileName = m.FileName,
                    ContentType = m.MediaType,
                    FileSize = m.FileSize
                }).ToList(),
                Certificates = certs.Select(m => new MediaItemDto
                {
                    Id = m.Id,
                    Url = m.FileUrl,
                    FileName = m.FileName,
                    ContentType = m.MediaType,
                    FileSize = m.FileSize
                }).ToList()
            };
        }

        public async Task<(bool ok, string message)> ApproveAsync(string userId)
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null || user.RoleName != "Tutor") return (false, "tài khoản không hợp lệ");

            var profile = await _uow.TutorProfiles.GetByUserIdAsync(userId);
            if (profile == null) return (false, "không tìm thấy hồ sơ gia sư");

            // Chỉ cho phép approve nếu đang PendingApproval
            if (user.Status != AccountStatus.PendingApproval || profile.ReviewStatus != ReviewStatus.Pending)
                return (false, "trạng thái không hợp lệ để duyệt");

            // Cập nhật đúng luồng bạn yêu cầu
            user.Status = AccountStatus.Active;
            user.UpdatedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(user);

            profile.ApprovedByAdmin = true;
            profile.ReviewStatus = ReviewStatus.Approved;
            profile.RejectReason = null;
            profile.ProvideNote = null;
            profile.UpdatedAt = DateTime.Now;
            await _uow.TutorProfiles.UpdateAsync(profile);

            await _uow.SaveChangesAsync();

            var notification = await _notificationService.CreateAccountNotificationAsync(
                user.Id,
                NotificationType.TutorApproved,
                relatedEntityId: profile.Id);
            await _uow.SaveChangesAsync();
            await _notificationService.SendRealTimeNotificationAsync(user.Id, notification);

            return (true, "duyệt tài khoản thành công");
        }

        public async Task<(bool ok, string message)> RejectAsync(string userId, string? rejectReason)
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null || user.RoleName != "Tutor") return (false, "tài khoản không hợp lệ");

            var profile = await _uow.TutorProfiles.GetByUserIdAsync(userId);
            if (profile == null) return (false, "không tìm thấy hồ sơ gia sư");

            // Chỉ cho phép reject nếu đang PendingApproval
            if (user.Status != AccountStatus.PendingApproval || profile.ReviewStatus != ReviewStatus.Pending)
                return (false, "trạng thái không hợp lệ để từ chối");

            user.Status = AccountStatus.Rejected;
            user.UpdatedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(user);

            profile.ApprovedByAdmin = false;
            profile.ReviewStatus = ReviewStatus.Rejected;
            profile.RejectReason = rejectReason;
            profile.ProvideNote = null;
            profile.UpdatedAt = DateTime.Now;
            await _uow.TutorProfiles.UpdateAsync(profile);

            await _uow.SaveChangesAsync();

            var notification = await _notificationService.CreateAccountNotificationAsync(
                user.Id,
                NotificationType.TutorRejected,
                reason: rejectReason,
                relatedEntityId: profile.Id);
            await _uow.SaveChangesAsync();
            await _notificationService.SendRealTimeNotificationAsync(user.Id, notification);

            return (true, "từ chối tài khoản thành công");
        }

        // Yêu cầu bổ sung hồ sơ (không đổi AccountStatus, vẫn PendingApproval)
        public async Task<(bool ok, string message)> ProvideAsync(string userId, string? note)
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null || user.RoleName != "Tutor")
                return (false, "tài khoản không hợp lệ");

            var profile = await _uow.TutorProfiles.GetByUserIdAsync(userId);
            if (profile == null) return (false, "không tìm thấy hồ sơ gia sư");

            if (user.Status != AccountStatus.PendingApproval || profile.ReviewStatus != ReviewStatus.Pending)
                return (false, "trạng thái không hợp lệ để yêu cầu bổ sung");

            profile.ReviewStatus = ReviewStatus.Pending;
            profile.ProvideNote = note;
            profile.UpdatedAt = DateTime.Now;

            await _uow.TutorProfiles.UpdateAsync(profile);
            await _uow.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(note))
            {
                var notification = await _notificationService.CreateSystemAnnouncementNotificationAsync(
                    user.Id,
                    "Yêu cầu bổ sung hồ sơ",
                    note,
                    relatedEntityId: profile.Id);
                await _uow.SaveChangesAsync();
                await _notificationService.SendRealTimeNotificationAsync(user.Id, notification);
            }

            return (true, "Đã yêu cầu bổ sung hồ sơ gia sư");
        }


        // === Bổ sung: List tất cả tutor phân trang ===
        public async Task<PaginationResult<TutorListItemDto>> GetTutorsPagedAsync(int page, int pageSize)
        {
            var result = await _uow.Users.GetPagedByRoleAsync("Tutor", page, pageSize);

            var mapped = result.Data.Select(u => new TutorListItemDto
            {
                TutorId = u.Id,
                Username = u.UserName,
                Email = u.Email!,
                Status = u.Status.ToString(),
                IsBanned = u.IsBanned || u.Status == AccountStatus.Banned,
                CreateDate = u.CreatedAt
            }).ToList();

            return new PaginationResult<TutorListItemDto>(mapped, result.TotalCount, result.PageNumber, result.PageSize);
        }
    }
}
