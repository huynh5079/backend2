using BusinessLayer.DTOs.Schedule.RescheduleRequest;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class LessonRescheduleService : ILessonRescheduleService
    {
        // Chỉ dùng IUnitOfWork, IScheduleUnitOfWork, và INotificationService
        private readonly IUnitOfWork _uow;
        private readonly IScheduleUnitOfWork _suow;
        private readonly INotificationService _notificationService;

        public LessonRescheduleService(IUnitOfWork uow, IScheduleUnitOfWork suow, INotificationService notificationService)
        {
            _uow = uow;
            _suow = suow;
            _notificationService = notificationService;
        }

        public async Task<RescheduleRequestDto> CreateRequestAsync(string tutorUserId, string lessonId, CreateRescheduleRequestDto dto)
        {
            // 1. Lấy thông tin cơ bản
            var tutorProfile = await _uow.TutorProfiles.GetAsync(p => p.UserId == tutorUserId)
                ?? throw new UnauthorizedAccessException("Không tìm thấy hồ sơ gia sư.");

            var lesson = await _suow.Lessons.GetAsync(
                filter: l => l.Id == lessonId,
                includes: q => q.Include(l => l.Class).Include(l => l.ScheduleEntries)
            ) ?? throw new KeyNotFoundException("Không tìm thấy buổi học.");

            var originalEntry = lesson.ScheduleEntries.FirstOrDefault()
                ?? throw new InvalidOperationException("Buổi học không có lịch (ScheduleEntry).");

            // 2. Kiểm tra quyền
            if (lesson.Class?.TutorId != tutorProfile.Id)
                throw new UnauthorizedAccessException("Bạn không phải là gia sư của lớp này.");

            if (lesson.Status != LessonStatus.SCHEDULED)
                throw new InvalidOperationException("Chỉ có thể đổi lịch các buổi học chưa diễn ra.");

            // 3. Kiểm tra logic thời gian
            if (dto.NewStartTime >= dto.NewEndTime)
                throw new ArgumentException("Giờ kết thúc mới phải sau giờ bắt đầu mới.");
            if (dto.NewStartTime <= DateTime.Now)
                throw new ArgumentException("Không thể đổi lịch về thời gian trong quá khứ.");

            // 4. KIỂM TRA XUNG ĐỘT LỊCH (Đã dùng Repository)
            var conflict = await _suow.ScheduleEntries.GetTutorConflictAsync(
                tutorProfile.Id, dto.NewStartTime.ToUniversalTime(), dto.NewEndTime.ToUniversalTime(), originalEntry.Id);

            if (conflict != null)
            {
                throw new InvalidOperationException($"Lịch đề xuất bị xung đột. Gia sư đã có lịch khác từ {conflict.StartTime} đến {conflict.EndTime}.");
            }

            // 5. Tạo yêu cầu (Transaction)
            await using var transaction = await _uow.BeginTransactionAsync();
            try
            {
                var newRequest = new RescheduleRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    RequesterUserId = tutorUserId,
                    LessonId = lessonId,
                    OriginalScheduleEntryId = originalEntry.Id,
                    OldStartTime = originalEntry.StartTime,
                    OldEndTime = originalEntry.EndTime,
                    NewStartTime = dto.NewStartTime.ToUniversalTime(),
                    NewEndTime = dto.NewEndTime.ToUniversalTime(),
                    Reason = dto.Reason,
                    Status = RescheduleStatus.Pending
                };
                await _uow.RescheduleRequests.CreateAsync(newRequest);

                // 6. Gửi thông báo (Dùng Repository)
                var participants = await _uow.ClassAssigns.GetParticipantsInClassAsync(lesson.ClassId);
                var tutorUser = await _uow.Users.GetByIdAsync(tutorUserId);
                var title = $"Yêu cầu đổi lịch từ Gia sư {tutorUser?.UserName}";
                var message = $"Gia sư đề xuất đổi lịch buổi học '{lesson.Title}' sang {dto.NewStartTime:dd/MM HH:mm}.";

                var notifications = new List<Notification>();
                foreach (var user in participants)
                {
                    var notif = await _notificationService.CreateSystemAnnouncementNotificationAsync(
                        user.Id, title, message, newRequest.Id);
                    // CreateSystemAnnouncementNotificationAsync đã gọi _uow.Notifications.AddAsync
                    notifications.Add(notif);
                }

                await _uow.SaveChangesAsync(); // Lưu cả Request và Notifications
                await transaction.CommitAsync();

                // 7. Gửi Real-time
                foreach (var notif in notifications)
                {
                    _ = _notificationService.SendRealTimeNotificationAsync(notif.UserId, notif);
                }

                return MapToDto(newRequest, tutorUser, null);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<RescheduleRequestDto> CreateRequestByStudentAsync(string actorUserId, string lessonId, CreateRescheduleRequestDto dto)
        {
            // 1. Lấy user & kiểm tra role
            var actorUser = await _uow.Users.GetByIdAsync(actorUserId)
                ?? throw new UnauthorizedAccessException("Không tìm thấy tài khoản của bạn.");

            if (actorUser.RoleName != "Student" && actorUser.RoleName != "Parent")
                throw new UnauthorizedAccessException("Chỉ học sinh hoặc phụ huynh mới được gửi yêu cầu đổi lịch.");

            // 2. Lấy lesson + class + scheduleEntry
            var lesson = await _suow.Lessons.GetAsync(
                filter: l => l.Id == lessonId,
                includes: q => q.Include(l => l.Class).Include(l => l.ScheduleEntries)
            ) ?? throw new KeyNotFoundException("Không tìm thấy buổi học.");

            if (lesson.Class == null)
                throw new InvalidOperationException("Buổi học không thuộc lớp nào.");

            var originalEntry = lesson.ScheduleEntries.FirstOrDefault()
                ?? throw new InvalidOperationException("Buổi học không có lịch (ScheduleEntry).");

            // 3. Kiểm tra actor có thuộc lớp này không
            bool isAllowed = false;
            if (actorUser.RoleName == "Student")
            {
                var stuProfileId = await _uow.StudentProfiles.GetIdByUserIdAsync(actorUserId);
                if (stuProfileId != null)
                {
                    isAllowed = await _uow.ClassAssigns.IsApprovedAsync(lesson.ClassId, stuProfileId);
                }
            }
            else if (actorUser.RoleName == "Parent")
            {
                var childIds = await _uow.ParentProfiles.GetChildrenIdsAsync(actorUserId);
                if (childIds.Any())
                {
                    isAllowed = await _uow.ClassAssigns.IsAnyChildApprovedAsync(lesson.ClassId, childIds);
                }
            }

            if (!isAllowed)
                throw new UnauthorizedAccessException("Bạn không có quyền đổi lịch cho buổi học này.");

            // 4. Kiểm tra trạng thái buổi học & thời gian
            if (lesson.Status != LessonStatus.SCHEDULED)
                throw new InvalidOperationException("Chỉ có thể đổi lịch các buổi học chưa diễn ra.");

            if (dto.NewStartTime >= dto.NewEndTime)
                throw new ArgumentException("Giờ kết thúc mới phải sau giờ bắt đầu mới.");
            if (dto.NewStartTime <= DateTime.Now)
                throw new ArgumentException("Không thể đổi lịch về thời gian trong quá khứ.");

            // 5. Kiểm tra xung đột lịch của GIA SƯ (vì thầy/cô vẫn phải rảnh khung mới)
            var tutorProfileId = lesson.Class.TutorId;
            if (string.IsNullOrEmpty(tutorProfileId))
                throw new InvalidOperationException("Không tìm thấy gia sư của lớp.");

            var conflict = await _suow.ScheduleEntries.GetTutorConflictAsync(
                tutorProfileId,
                dto.NewStartTime.ToUniversalTime(),
                dto.NewEndTime.ToUniversalTime(),
                originalEntry.Id);

            if (conflict != null)
            {
                throw new InvalidOperationException($"Lịch đề xuất bị xung đột. Gia sư đã có lịch khác từ {conflict.StartTime} đến {conflict.EndTime}.");
            }

            // 6. Tạo request + gửi thông báo cho Gia sư
            await using var transaction = await _uow.BeginTransactionAsync();
            try
            {
                var newRequest = new RescheduleRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    RequesterUserId = actorUserId,           // Lúc này người yêu cầu là Student/Parent
                    LessonId = lessonId,
                    OriginalScheduleEntryId = originalEntry.Id,
                    OldStartTime = originalEntry.StartTime,
                    OldEndTime = originalEntry.EndTime,
                    NewStartTime = dto.NewStartTime.ToUniversalTime(),
                    NewEndTime = dto.NewEndTime.ToUniversalTime(),
                    Reason = dto.Reason,
                    Status = RescheduleStatus.Pending
                };
                await _uow.RescheduleRequests.CreateAsync(newRequest);

                // Tìm user của gia sư
                var tutorProfile = await _uow.TutorProfiles.GetByIdAsync(lesson.Class.TutorId)
                    ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ gia sư của lớp.");
                var tutorUser = await _uow.Users.GetByIdAsync(tutorProfile.UserId)
                    ?? throw new KeyNotFoundException("Không tìm thấy tài khoản gia sư.");

                var title = $"Yêu cầu đổi lịch từ học sinh {actorUser.UserName}";
                var message = $"Học sinh đề xuất đổi lịch buổi học '{lesson.Title}' sang {dto.NewStartTime:dd/MM HH:mm}.";

                var notifications = new List<Notification>();
                var notif = await _notificationService.CreateSystemAnnouncementNotificationAsync(
                    tutorUser.Id, title, message, newRequest.Id);
                notifications.Add(notif);

                await _uow.SaveChangesAsync();
                await transaction.CommitAsync();

                // Realtime cho gia sư
                foreach (var n in notifications)
                {
                    _ = _notificationService.SendRealTimeNotificationAsync(n.UserId, n);
                }

                return MapToDto(newRequest, actorUser, tutorUser);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<RescheduleRequestDto> AcceptRequestAsync(string actorUserId, string requestId)
        {
            // Gọi hàm xử lý chung với accept: true
            return await HandleResponseAsync(actorUserId, requestId, accept: true);
        }

        public async Task<RescheduleRequestDto> RejectRequestAsync(string actorUserId, string requestId)
        {
            // Gọi hàm xử lý chung với accept: false
            return await HandleResponseAsync(actorUserId, requestId, accept: false);
        }

        /// <summary>
        /// Hàm private xử lý logic chung cho Accept/Reject
        /// </summary>
        private async Task<RescheduleRequestDto> HandleResponseAsync(string actorUserId, string requestId, bool accept)
        {
            var actorUser = await _uow.Users.GetByIdAsync(actorUserId)
                ?? throw new UnauthorizedAccessException("Không tìm thấy tài khoản của bạn.");

            var request = await _uow.RescheduleRequests.GetAsync(
                filter: r => r.Id == requestId,
                includes: q => q
                    .Include(r => r.Lesson).ThenInclude(l => l.Class)
                    .Include(r => r.RequesterUser)
            ) ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đổi lịch.");

            if (request.Status != RescheduleStatus.Pending)
                throw new InvalidOperationException("Yêu cầu này đã được xử lý.");

            if (request.RequesterUserId == actorUserId)
                throw new InvalidOperationException("Bạn không thể tự phản hồi yêu cầu do chính mình tạo.");

            // 7. Kiểm tra quyền của người phản hồi
            bool isAllowed = false;
            var requesterRole = request.RequesterUser.RoleName;

            // Trường hợp request được tạo bởi GIA SƯ -> chỉ Student/Parent trong lớp được trả lời
            if (requesterRole == "Tutor")
            {
                if (actorUser.RoleName == "Student")
                {
                    var stuProfileId = await _uow.StudentProfiles.GetIdByUserIdAsync(actorUserId);
                    if (stuProfileId != null)
                    {
                        isAllowed = await _uow.ClassAssigns.IsApprovedAsync(request.Lesson.ClassId, stuProfileId);
                    }
                }
                else if (actorUser.RoleName == "Parent")
                {
                    var childIds = await _uow.ParentProfiles.GetChildrenIdsAsync(actorUserId);
                    if (childIds.Any())
                    {
                        isAllowed = await _uow.ClassAssigns.IsAnyChildApprovedAsync(request.Lesson.ClassId, childIds);
                    }
                }
            }
            // Trường hợp request được tạo bởi Học sinh/Phụ huynh -> chỉ Gia sư của lớp được trả lời
            else if (requesterRole == "Student" || requesterRole == "Parent")
            {
                if (actorUser.RoleName == "Tutor")
                {
                    var tutorProfileId = await _uow.TutorProfiles.GetIdByUserIdAsync(actorUserId)
                        ?? throw new UnauthorizedAccessException("Không tìm thấy hồ sơ gia sư.");

                    isAllowed = request.Lesson.Class?.TutorId == tutorProfileId;
                }
            }

            if (!isAllowed)
                throw new UnauthorizedAccessException("Bạn không có quyền phản hồi yêu cầu này.");

            // 8. Bắt đầu Transaction
            await using var transaction = await _uow.BeginTransactionAsync();
            try
            {
                request.Status = accept ? RescheduleStatus.Accepted : RescheduleStatus.Rejected;
                request.ResponderUserId = actorUserId;
                request.RespondedAt = DateTime.Now;
                await _uow.RescheduleRequests.UpdateAsync(request);

                string title, message;
                NotificationType notifType;

                var actorTypeText = actorUser.RoleName == "Tutor" ? "Gia sư" : "Học sinh";

                if (accept)
                {
                    // Cập nhật lịch gốc như cũ
                    var originalEntry = await _suow.ScheduleEntries.GetByIdAsync(request.OriginalScheduleEntryId)
                        ?? throw new KeyNotFoundException("Không tìm thấy lịch học gốc để cập nhật.");

                    var conflict = await _suow.ScheduleEntries.GetTutorConflictAsync(
                        originalEntry.TutorId, request.NewStartTime, request.NewEndTime, originalEntry.Id);

                    if (conflict != null)
                    {
                        throw new InvalidOperationException($"Không thể chấp nhận. Lịch mới bị xung đột với một lịch khác của gia sư.");
                    }

                    originalEntry.StartTime = request.NewStartTime;
                    originalEntry.EndTime = request.NewEndTime;
                    await _suow.ScheduleEntries.UpdateAsync(originalEntry);

                    title = "Yêu cầu đổi lịch ĐƯỢC CHẤP NHẬN";
                    message = $"{actorTypeText} {actorUser.UserName} đã đồng ý đổi lịch buổi '{request.Lesson.Title}' sang {request.NewStartTime:dd/MM HH:mm}.";
                    notifType = NotificationType.LessonRescheduleAccepted;
                }
                else
                {
                    title = "Yêu cầu đổi lịch BỊ TỪ CHỐI";
                    message = $"{actorTypeText} {actorUser.UserName} đã từ chối đổi lịch buổi '{request.Lesson.Title}'.";
                    notifType = NotificationType.LessonRescheduleRejected;
                }

                // Thông báo gửi ngược lại cho người tạo request
                var tutorNotif = await _notificationService.CreateSystemAnnouncementNotificationAsync(
                    request.RequesterUserId, title, message, request.Id);

                await _uow.SaveChangesAsync();
                await _suow.SaveChangesAsync();

                await transaction.CommitAsync();

                _ = _notificationService.SendRealTimeNotificationAsync(tutorNotif.UserId, tutorNotif);

                return MapToDto(request, request.RequesterUser, actorUser);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<RescheduleRequestDto>> GetPendingRequestsAsync(string actorUserId)
        {
            var user = await _uow.Users.GetByIdAsync(actorUserId)
                ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

            List<RescheduleRequest> requests;

            if (user.RoleName == "Tutor")
            {
                var tutorProfileId = await _uow.TutorProfiles.GetIdByUserIdAsync(actorUserId)
                    ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ gia sư.");

                requests = (await _uow.RescheduleRequests.GetAllAsync(
                    filter: r => r.Status == RescheduleStatus.Pending
                                 && r.Lesson.Class != null
                                 && r.Lesson.Class.TutorId == tutorProfileId,
                    includes: q => q
                        .Include(r => r.RequesterUser)
                        .Include(r => r.ResponderUser)
                        .Include(r => r.Lesson).ThenInclude(l => l.Class)
                )).ToList();
            }
            else
            {
                var studentProfileIds = new List<string>();
                if (user.RoleName == "Student")
                {
                    var spId = await _uow.StudentProfiles.GetIdByUserIdAsync(actorUserId);
                    if (spId != null) studentProfileIds.Add(spId);
                }
                else if (user.RoleName == "Parent")
                {
                    studentProfileIds = await _uow.ParentProfiles.GetChildrenIdsAsync(actorUserId);
                }

                if (!studentProfileIds.Any()) return new List<RescheduleRequestDto>();

                var assignments = await _uow.ClassAssigns.GetAllAsync(
                    filter: ca => studentProfileIds.Contains(ca.StudentId) && ca.ApprovalStatus == ApprovalStatus.Approved
                );
                var classIds = assignments.Select(ca => ca.ClassId).Distinct().ToList();

                requests = (await _uow.RescheduleRequests.GetAllAsync(
                    filter: r => r.Lesson.ClassId != null &&
                                 classIds.Contains(r.Lesson.ClassId) && // 'classIds' đã tồn tại
                                 r.Status == RescheduleStatus.Pending,
                    includes: q => q.Include(r => r.RequesterUser).Include(r => r.ResponderUser)
                )).ToList();
            }

            return requests.Select(r => MapToDto(r, r.RequesterUser, r.ResponderUser!));
        }

        // --- Helper ---
        private RescheduleRequestDto MapToDto(RescheduleRequest r, User? requester, User? responder)
        {
            return new RescheduleRequestDto
            {
                Id = r.Id,
                LessonId = r.LessonId,
                RequesterUserId = r.RequesterUserId,
                RequesterName = requester?.UserName ?? "N/A",
                OldStartTime = r.OldStartTime,
                OldEndTime = r.OldEndTime,
                NewStartTime = r.NewStartTime,
                NewEndTime = r.NewEndTime,
                Reason = r.Reason,
                Status = r.Status,
                ResponderUserId = r.ResponderUserId,
                ResponderName = responder?.UserName,
                RespondedAt = r.RespondedAt
            };
        }
    }
}
