using BusinessLayer.DTOs.Feedback;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUnitOfWork _uow;
        private readonly TpeduContext _ctx;
        private readonly INotificationService _notificationService;

        public FeedbackService(IUnitOfWork uow, TpeduContext ctx, INotificationService notificationService)
        {
            _uow = uow;
            _ctx = ctx;
            _notificationService = notificationService;
        }

        public async Task<FeedbackDto> CreateAsync(string actorUserId, CreateFeedbackRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ClassId) || string.IsNullOrWhiteSpace(req.ToUserId))
                throw new ArgumentException("Thiếu ClassId/ToUserId");
            if (req.Rating is < 0 or > 5)
                throw new ArgumentException("Rating phải trong khoảng 0..5");

            // 1. Lấy Class
            var cls = await _ctx.Classes
                .Include(c => c.Tutor).ThenInclude(t => t.User)
                .Include(c => c.ClassAssigns).ThenInclude(ca => ca.Student).ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.Id == req.ClassId)
                ?? throw new KeyNotFoundException("Không tìm thấy lớp học");

            // 2. Lấy TutorProfile để map sang UserId
            var tutorProfileId = cls.TutorId
                               ?? throw new InvalidOperationException("Lớp học chưa gắn gia sư");

            var tutorProfile = await _uow.TutorProfiles.GetAsync(p => p.Id == tutorProfileId)
                               ?? throw new KeyNotFoundException($"Không tìm thấy TutorProfile (Id={tutorProfileId})");

            var actualTutorUserId = tutorProfile.UserId;

            // 3. Lấy actor
            var actor = await _uow.Users.GetByIdAsync(actorUserId)
                        ?? throw new KeyNotFoundException("Không tìm thấy user");

            // 4. Check quyền: actor có thuộc lớp & toUser hợp lệ không
            await EnsureClassParticipantPermission(actor, req.ToUserId, cls, actualTutorUserId);

            // 5. Check trùng feedback trong cùng class
            var exists = await _uow.Feedbacks.ExistsAsync(actorUserId, req.ToUserId, req.ClassId);
            if (exists) throw new InvalidOperationException("Bạn đã gửi feedback cho lớp học này.");

            // 6. Tạo feedback
            var fb = new Feedback
            {
                FromUserId = actorUserId,
                ToUserId = req.ToUserId,
                ClassId = req.ClassId,
                Rating = req.Rating,
                Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(),
                IsPublicOnTutorProfile = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _uow.Feedbacks.CreateAsync(fb);
            await _uow.SaveChangesAsync();

            await RecalculateTutorRatingAsync(req.ToUserId);

            // Gửi notification cho người nhận feedback (thường là tutor)
            if (!string.IsNullOrEmpty(req.ToUserId) && req.ToUserId != actorUserId)
            {
                try
                {
                    var ratingText = req.Rating.HasValue ? $"{req.Rating.Value}/5 sao" : "không có đánh giá";
                    var message = $"Bạn có đánh giá mới từ {actor.UserName ?? "học sinh"}: {ratingText}.{(string.IsNullOrWhiteSpace(req.Comment) ? "" : $" \"{req.Comment}\"")}";
                    var notification = await _notificationService.CreateAccountNotificationAsync(
                        req.ToUserId,
                        NotificationType.FeedbackCreated,
                        message,
                        req.ClassId);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(req.ToUserId, notification);
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                }
            }

            var fromName = actor.UserName ?? "User";
            var toName = (await _uow.Users.GetByIdAsync(req.ToUserId))?.UserName ?? "User";
            return MapDto(fb, fromName, toName);
        }

        public async Task<FeedbackDto> CreateForTutorProfileAsync(string actorUserId, string tutorUserId, CreateTutorProfileFeedbackRequest req)
        {
            if (string.IsNullOrWhiteSpace(tutorUserId))
                throw new ArgumentException("Thiếu TutorUserId");
            if (req.Rating is < 0 or > 5)
                throw new ArgumentException("Rating phải trong khoảng 0..5");

            var actor = await _uow.Users.GetByIdAsync(actorUserId)
                        ?? throw new KeyNotFoundException("Không tìm thấy user");
            var tutor = await _uow.Users.GetByIdAsync(tutorUserId)
                        ?? throw new KeyNotFoundException("Không tìm thấy tutor");
            if (tutor.RoleName != "Tutor")
                throw new InvalidOperationException("TutorUserId không phải tài khoản Tutor");

            // Cho phép nhiều bài viết công khai theo thời gian (LessonId = null)
            var fb = new Feedback
            {
                FromUserId = actorUserId,
                ToUserId = tutorUserId,
                LessonId = null,                                  // ✅ không gắn buổi
                IsPublicOnTutorProfile = true,                    // ✅ render trên trang Tutor
                Rating = req.Rating,                              // sẽ KHÔNG tính vào rating tổng
                Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _uow.Feedbacks.CreateAsync(fb);
            await _uow.SaveChangesAsync();

            // Gửi notification cho tutor khi có feedback công khai mới
            if (!string.IsNullOrEmpty(tutorUserId) && tutorUserId != actorUserId)
            {
                try
                {
                    var ratingText = req.Rating.HasValue ? $"{req.Rating.Value}/5 sao" : "không có đánh giá";
                    var message = $"Bạn có đánh giá công khai mới từ {actor.UserName ?? "người dùng"}: {ratingText}.{(string.IsNullOrWhiteSpace(req.Comment) ? "" : $" \"{req.Comment}\"")}";
                    var notification = await _notificationService.CreateAccountNotificationAsync(
                        tutorUserId,
                        NotificationType.FeedbackCreated,
                        message,
                        null);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(tutorUserId, notification);
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                }
            }

            var fromName = actor.UserName ?? "User";
            var toName = tutor.UserName ?? "Tutor";
            return MapDto(fb, fromName, toName);
        }

        public async Task<FeedbackDto> UpdateAsync(string actorUserId, string feedbackId, UpdateFeedbackRequest req)
        {
            var fb = await _uow.Feedbacks.GetByIdAsync(feedbackId)
                     ?? throw new KeyNotFoundException("Không tìm thấy feedback");

            if (fb.FromUserId != actorUserId)
                throw new UnauthorizedAccessException("Chỉ người viết feedback mới được sửa");

            if (req.Rating is < 0 or > 5)
                throw new ArgumentException("Rating phải trong khoảng 0..5");

            if (req.Rating.HasValue) fb.Rating = req.Rating.Value;
            if (req.Comment != null) fb.Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
            fb.UpdatedAt = DateTime.Now;

            await _uow.Feedbacks.UpdateAsync(fb);
            await _uow.SaveChangesAsync();

            // ✅ Chỉ lesson feedback (fb.LessonId != null) mới kéo theo tính lại rating tổng
            if (!string.IsNullOrEmpty(fb.ToUserId) && fb.LessonId != null)
            {
                await RecalculateTutorRatingAsync(fb.ToUserId!);
            }

            var fromName = (await _uow.Users.GetByIdAsync(fb.FromUserId!))?.UserName ?? "User";
            var toName = (await _uow.Users.GetByIdAsync(fb.ToUserId!))?.UserName ?? "User";
            return MapDto(fb, fromName, toName);
        }

        public async Task<bool> DeleteAsync(string actorUserId, string feedbackId)
        {
            var fb = await _uow.Feedbacks.GetByIdAsync(feedbackId)
                     ?? throw new KeyNotFoundException("Không tìm thấy feedback");

            if (fb.FromUserId != actorUserId)
                throw new UnauthorizedAccessException("Chỉ người viết feedback mới được xóa");

            await _uow.Feedbacks.RemoveAsync(fb);
            await _uow.SaveChangesAsync();

            // Nếu ToUser là tutor -> cập nhật rating
            await RecalculateTutorRatingAsync(fb.ToUserId!);

            return true;
        }

        public async Task<IEnumerable<FeedbackDto>> GetClassFeedbacksAsync(string classId)
        {
            var items = await _uow.Feedbacks.GetByClassAsync(classId);
            return items.Select(f => MapDto(
                f,
                f.FromUser?.UserName ?? "User",
                f.ToUser?.UserName ?? "User"));
        }

        public async Task<(IEnumerable<FeedbackDto> items, int total)> GetTutorFeedbacksAsync(string tutorUserId, int page, int pageSize)
        {
            var (items, total) = await _uow.Feedbacks.GetByTutorUserAsync(tutorUserId, page, pageSize);
            var list = items.Select(f => MapDto(f,
                f.FromUser?.UserName ?? "User",
                f.ToUser?.UserName ?? "User")).ToList();
            return (list, total);
        }

        public async Task<TutorRatingSummaryDto> GetTutorRatingAsync(string tutorUserId)
        {
            var (avg, count) = await _uow.Feedbacks.CalcTutorRatingAsync(tutorUserId);
            return new TutorRatingSummaryDto
            {
                TutorUserId = tutorUserId,
                Average = Math.Round(avg, 2),
                Count = count
            };
        }

        // ===== Helpers =====

        private async Task EnsureClassParticipantPermission(User actor, string toUserId, Class cls, string tutorUserId)
        {
            var actorRole = actor.RoleName;

            // A. Tutor đánh giá học sinh
            if (actorRole == "Tutor")
            {
                var tutorProfile = await _uow.TutorProfiles.GetAsync(t => t.UserId == actor.Id)
                                    ?? throw new InvalidOperationException("Không tìm thấy profile Tutor");

                if (tutorProfile.Id != cls.TutorId)
                    throw new UnauthorizedAccessException("Tutor không sở hữu lớp này");

                var isStudentInClass = cls.ClassAssigns.Any(ca =>
                    ca.Student != null &&
                    ca.Student.User != null &&
                    ca.Student.User.Id == toUserId &&
                    ca.ApprovalStatus == ApprovalStatus.Approved);

                if (!isStudentInClass)
                    throw new InvalidOperationException("Người nhận không thuộc lớp này");

                return;
            }

            // B. Student đánh giá Tutor
            if (actorRole == "Student")
            {
                var stuProfile = await _uow.StudentProfiles.GetAsync(s => s.UserId == actor.Id)
                                 ?? throw new InvalidOperationException("Không tìm thấy profile HS");

                var isInClass = cls.ClassAssigns.Any(ca =>
                    ca.StudentId == stuProfile.Id &&
                    ca.ApprovalStatus == ApprovalStatus.Approved);

                if (!isInClass)
                    throw new UnauthorizedAccessException("Bạn không thuộc lớp học này");

                if (toUserId != tutorUserId)
                    throw new InvalidOperationException("Student chỉ có thể đánh giá Tutor của lớp");

                return;
            }

            // C. Parent đánh giá Tutor
            if (actorRole == "Parent")
            {
                var childIds = await _uow.ParentProfiles.GetChildrenIdsAsync(actor.Id);

                var hasChildInClass = cls.ClassAssigns.Any(ca =>
                    ca.StudentId != null &&
                    childIds.Contains(ca.StudentId) &&
                    ca.ApprovalStatus == ApprovalStatus.Approved);

                if (!hasChildInClass)
                    throw new UnauthorizedAccessException("Phụ huynh không có học sinh trong lớp này");

                if (toUserId != tutorUserId)
                    throw new InvalidOperationException("Parent chỉ có thể đánh giá Tutor của lớp");

                return;
            }

            throw new UnauthorizedAccessException("Bạn không có quyền gửi feedback cho lớp này");
        }

        private async Task RecalculateTutorRatingAsync(string toUserId)
        {
            // Nếu người nhận là Tutor (User.RoleName == Tutor) -> ghi vào TutorProfile.Rating
            var toUser = await _uow.Users.GetByIdAsync(toUserId);
            if (toUser == null || toUser.RoleName != "Tutor") return;

            var (avg, _) = await _uow.Feedbacks.CalcTutorRatingAsync(toUserId);
            // 1 TutorUser : nhiều TutorProfile (theo db) -> lấy profile đầu?
            var profile = await _uow.TutorProfiles.GetAsync(t => t.UserId == toUserId);
            if (profile != null)
            {
                profile.Rating = Math.Round(avg, 2);
                await _uow.TutorProfiles.UpdateAsync(profile);
                await _uow.SaveChangesAsync();
            }
        }

        private static FeedbackDto MapDto(Feedback f, string fromName, string toName)
        {
            return new FeedbackDto
            {
                Id = f.Id,
                ClassId = f.ClassId ?? "",
                FromUserId = f.FromUserId ?? "",
                FromUserName = fromName,
                ToUserId = f.ToUserId ?? "",
                ToUserName = toName,
                Rating = f.Rating,
                Comment = f.Comment,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            };
        }
    }
}
