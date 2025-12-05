using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.DTOs.Schedule.ClassAssign;
using BusinessLayer.DTOs.Wallet;
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
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class AssignService : IAssignService
    {
        private readonly TpeduContext _context;
        private readonly IScheduleUnitOfWork _uow;
        private readonly IUnitOfWork _mainUow;
        private readonly IStudentProfileService _studentProfileService;
        private readonly ITutorProfileService _tutorProfileService;
        private readonly IScheduleGenerationService _scheduleGenerationService;
        private readonly IEscrowService _escrowService;
        private readonly INotificationService _notificationService;

        public AssignService(
            TpeduContext context,
            IScheduleUnitOfWork uow,
            IUnitOfWork mainUow,
            IStudentProfileService studentProfileService,
            ITutorProfileService tutorProfileService,
            IScheduleGenerationService scheduleGenerationService,
            IEscrowService escrowService,
            INotificationService notificationService)
        {
            _context = context;
            _uow = uow;
            _mainUow = mainUow;
            _studentProfileService = studentProfileService;
            _tutorProfileService = tutorProfileService;
            _scheduleGenerationService = scheduleGenerationService;
            _escrowService = escrowService;
            _notificationService = notificationService;
        }

        public async Task<ClassDto> AssignRecurringClassAsync(string studentUserId, string classId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            Class targetClass = null;
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // load Class with Schedules
                    targetClass = await _uow.Classes.GetAsync(
                        filter: c => c.Id == classId,
                        includes: q => q.Include(c => c.ClassSchedules) // <-- Load các Rules
                    );

                    // Validation
                    if (targetClass == null)
                        throw new KeyNotFoundException($"Không tìm thấy lớp học với ID '{classId}'.");

                    // only Pending or Active classes can be assigned
                    if (targetClass.Status != ClassStatus.Pending && targetClass.Status != ClassStatus.Active)
                        throw new InvalidOperationException($"Không thể ghi danh. Lớp học này đang ở trạng thái '{targetClass.Status}'.");

                    if (targetClass.CurrentStudentCount >= targetClass.StudentLimit)
                        throw new InvalidOperationException("Lớp học đã đủ số lượng học sinh.");

                    // check if already assigned
                    var existingAssignment = await _uow.ClassAssigns.GetAsync(
                        a => a.StudentId == studentProfileId && a.ClassId == classId);

                    if (existingAssignment != null)
                        throw new InvalidOperationException("Bạn đã ghi danh vào lớp học này rồi.");

                    // Check giới hạn học sinh TRƯỚC KHI tạo ClassAssign
                    if (targetClass.CurrentStudentCount >= targetClass.StudentLimit)
                        throw new InvalidOperationException("Lớp học đã đủ số lượng học sinh.");

                    // create ClassAssign
                    var newAssignment = new ClassAssign
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = classId,
                        StudentId = studentProfileId,
                        PaymentStatus = PaymentStatus.Pending, // Chưa thanh toán - sẽ thanh toán qua /escrow/pay
                        ApprovalStatus = ApprovalStatus.Approved, // Tutor tạo lớp = auto accept học sinh
                        EnrolledAt = DateTime.UtcNow
                    };
                    await _uow.ClassAssigns.CreateAsync(newAssignment); // non save

                    // update Class
                    targetClass.CurrentStudentCount++;
                    // KHÔNG set Ongoing ngay - chỉ set khi đã thanh toán và đặt cọc
                    // Giữ nguyên status hiện tại (Pending hoặc Active) - chờ thanh toán

                    await _uow.Classes.UpdateAsync(targetClass); // non save

                    // create Schedule/Lesson if first student
                    if (targetClass.CurrentStudentCount == 1)
                    {
                        await _scheduleGenerationService.GenerateScheduleFromClassAsync(
                            targetClass.Id,
                            targetClass.TutorId,
                            targetClass.ClassStartDate ?? DateTime.UtcNow,
                            targetClass.ClassSchedules // include rules
                        );
                    }

                    // save all
                    await _uow.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Gửi notification cho student khi enroll thành công
                    var studentProfile = await _mainUow.StudentProfiles.GetAsync(
                        filter: s => s.Id == studentProfileId,
                        includes: q => q.Include(s => s.User));
                    if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId))
                    {
                        try
                        {
                            var notification = await _notificationService.CreateAccountNotificationAsync(
                                studentProfile.UserId,
                                NotificationType.ClassEnrollmentSuccess,
                                $"Bạn đã ghi danh vào lớp học '{targetClass.Title}' thành công.",
                                targetClass.Id);
                            await _uow.SaveChangesAsync();
                            await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, notification);
                        }
                        catch (Exception notifEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                        }
                    }

                    // Gửi notification cho tutor khi có học sinh mới enroll vào lớp
                    var tutorProfile = await _mainUow.TutorProfiles.GetByIdAsync(targetClass.TutorId);
                    if (tutorProfile != null && !string.IsNullOrEmpty(tutorProfile.UserId))
                    {
                        try
                        {
                            var studentName = studentProfile?.User?.UserName ?? "một học sinh";
                            var notification = await _notificationService.CreateAccountNotificationAsync(
                                tutorProfile.UserId,
                                NotificationType.StudentEnrolledInClass,
                                $"{studentName} đã ghi danh vào lớp học '{targetClass.Title}' của bạn.",
                                targetClass.Id);
                            await _uow.SaveChangesAsync();
                            await _notificationService.SendRealTimeNotificationAsync(tutorProfile.UserId, notification);
                        }
                        catch (Exception notifEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                        }
                    }
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
            return MapToClassDto(targetClass!);
        }

        /// <summary>
        /// [TRANSACTION] Student withdraw from class - Học sinh hủy enrollment
        /// Xử lý refund escrow cho học sinh khi rút khỏi lớp
        /// </summary>
        public async Task<bool> WithdrawFromClassAsync(string studentUserId, string classId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // load ClassAssign
                    var assignment = await _uow.ClassAssigns.GetAsync(
                        a => a.StudentId == studentProfileId && a.ClassId == classId);

                    if (assignment == null)
                        throw new KeyNotFoundException("Bạn chưa ghi danh vào lớp học này.");

                    // load Class
                    var targetClass = await _uow.Classes.GetByIdAsync(classId);
                    if (targetClass == null)
                        throw new KeyNotFoundException("Không tìm thấy lớp học.");

                    // validate Class status
                    if (targetClass.Status == ClassStatus.Completed ||
                        targetClass.Status == ClassStatus.Cancelled)
                    {
                        throw new InvalidOperationException($"Không thể rút khỏi lớp học đang ở trạng thái '{targetClass.Status}'.");
                    }

                    // Xử lý refund escrow cho học sinh khi rút khỏi lớp
                    // Refund full (100%) cho học sinh
                    var escrows = await _mainUow.Escrows.GetAllAsync(
                        filter: e => e.ClassAssignId == assignment.Id && 
                                   (e.Status == EscrowStatus.Held || e.Status == EscrowStatus.PartiallyReleased));

                    foreach (var esc in escrows)
                    {
                        if (esc.Status == EscrowStatus.Held)
                        {
                            // Refund full cho học sinh
                            await _escrowService.RefundAsync(studentUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                        }
                        else if (esc.Status == EscrowStatus.PartiallyReleased)
                        {
                            // Đã release một phần cho tutor → Refund phần còn lại (100% của phần còn lại)
                            decimal remainingPercentage = 1.0m - (esc.ReleasedAmount / esc.GrossAmount);
                            if (remainingPercentage > 0)
                            {
                                await _escrowService.PartialRefundAsync(studentUserId, new PartialRefundEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    RefundPercentage = remainingPercentage
                                });
                            }
                        }
                    }

                    // Cập nhật PaymentStatus của ClassAssign
                    assignment.PaymentStatus = PaymentStatus.Refunded;
                    await _uow.ClassAssigns.UpdateAsync(assignment);

                    // delete ClassAssign
                    _context.ClassAssigns.Remove(assignment); //using _context to avoid tracking issues

                    // update Class, decrease student count
                    if (targetClass.CurrentStudentCount > 0)
                    {
                        targetClass.CurrentStudentCount--;
                    }
                    else
                    {
                        // If CurrentStudentCount is already 0
                        // Change to 0 to avoid negative values
                        targetClass.CurrentStudentCount = 0;
                    }
                    // if no students left, set to Pending and clean up future Lessons/Schedules
                    if (targetClass.CurrentStudentCount == 0)
                    {
                        targetClass.Status = ClassStatus.Cancelled;

                        // find and delete future ScheduleEntries and Lessons
                        // include Lesson when querying ScheduleEntries
                        var futureEntries = await _context.ScheduleEntries
                            .Include(se => se.Lesson)
                            .Where(se => se.Lesson.ClassId == classId && se.StartTime > DateTime.Now)
                            .ToListAsync();

                        if (futureEntries.Any())
                        {
                            // take lessonids of future entries to delete lessons
                            // use Distinct to avoid duplicates
                            var futureLessonIds = futureEntries
                                .Select(se => se.LessonId)
                                .Where(id => id != null)
                                .Distinct()
                                .ToList();

                            // delete future ScheduleEntries first (FK constraints)
                            _context.ScheduleEntries.RemoveRange(futureEntries);

                            // find and delete future Lessons related
                            if (futureLessonIds.Any())
                            {
                                var futureLessons = await _context.Lessons
                                    .Where(l => futureLessonIds.Contains(l.Id))
                                    .ToListAsync();

                                _context.Lessons.RemoveRange(futureLessons);
                            }
                        }
                    }
                    await _uow.Classes.UpdateAsync(targetClass); // non save

                    // save all
                    await _uow.SaveChangesAsync();
                    await _mainUow.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            return true;
        }

        public async Task<List<MyEnrolledClassesDto>> GetMyEnrolledClassesAsync(string studentUserId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var classAssigns = await _uow.ClassAssigns.GetByStudentIdAsync(studentProfileId, includeClass: true);

            return classAssigns.Select(ca => new MyEnrolledClassesDto
            {
                ClassId = ca.ClassId ?? string.Empty,
                ClassTitle = ca.Class?.Title ?? "N/A",
                Subject = ca.Class?.Subject,
                EducationLevel = ca.Class?.EducationLevel,
                TutorName = ca.Class?.Tutor?.User?.UserName ?? "N/A",
                Price = ca.Class?.Price ?? 0,
                ClassStatus = ca.Class?.Status ?? ClassStatus.Pending,
                ApprovalStatus = ca.ApprovalStatus,
                PaymentStatus = ca.PaymentStatus,
                EnrolledAt = ca.EnrolledAt,
                Location = ca.Class?.Location,
                Mode = ca.Class?.Mode ?? ClassMode.Offline,
                ClassStartDate = ca.Class?.ClassStartDate
            }).ToList();
        }

        public async Task<EnrollmentCheckDto> CheckEnrollmentAsync(string studentUserId, string classId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var isEnrolled = await _uow.ClassAssigns.IsApprovedAsync(classId, studentProfileId);

            return new EnrollmentCheckDto
            {
                ClassId = classId,
                IsEnrolled = isEnrolled
            };
        }

        public async Task<List<StudentEnrollmentDto>> GetStudentsInClassAsync(string tutorUserId, string classId)
        {
            // Verify tutor owns the class
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            var targetClass = await _uow.Classes.GetByIdAsync(classId);
            if (targetClass == null)
                throw new KeyNotFoundException($"Không tìm thấy lớp học với ID '{classId}'.");

            if (targetClass.TutorId != tutorProfileId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem học sinh của lớp học này.");

            // Get all enrollments for this class
            var classAssigns = await _uow.ClassAssigns.GetByClassIdAsync(classId, includeStudent: true);

            return classAssigns.Select(ca => new StudentEnrollmentDto
            {
                StudentId = ca.StudentId ?? string.Empty,
                StudentName = ca.Student?.User?.UserName ?? "N/A",
                StudentEmail = ca.Student?.User?.Email,
                StudentAvatarUrl = ca.Student?.User?.AvatarUrl,
                StudentPhone = ca.Student?.User?.Phone,
                ApprovalStatus = ca.ApprovalStatus,
                PaymentStatus = ca.PaymentStatus,
                EnrolledAt = ca.EnrolledAt,
                CreatedAt = ca.CreatedAt
            }).ToList();
        }

        public async Task<ClassAssignDetailDto> GetEnrollmentDetailAsync(string userId, string classId)
        {
            // Student/Parent can only view their own enrollment
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(userId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var classAssign = await _uow.ClassAssigns.GetByClassAndStudentAsync(classId, studentProfileId, includeClass: true);
            if (classAssign == null)
                throw new KeyNotFoundException("Bạn chưa ghi danh vào lớp học này.");

            return new ClassAssignDetailDto
            {
                ClassAssignId = classAssign.Id,
                ClassId = classAssign.ClassId ?? string.Empty,
                ClassTitle = classAssign.Class?.Title ?? "N/A",
                ClassDescription = classAssign.Class?.Description,
                ClassSubject = classAssign.Class?.Subject,
                ClassEducationLevel = classAssign.Class?.EducationLevel,
                ClassPrice = classAssign.Class?.Price ?? 0,
                ClassStatus = classAssign.Class?.Status ?? ClassStatus.Pending,
                StudentId = classAssign.StudentId ?? string.Empty,
                StudentName = classAssign.Student?.User?.UserName ?? "N/A",
                StudentEmail = classAssign.Student?.User?.Email,
                StudentPhone = classAssign.Student?.User?.Phone,
                StudentAvatarUrl = classAssign.Student?.User?.AvatarUrl,
                ApprovalStatus = classAssign.ApprovalStatus,
                PaymentStatus = classAssign.PaymentStatus,
                EnrolledAt = classAssign.EnrolledAt,
                CreatedAt = classAssign.CreatedAt,
                UpdatedAt = classAssign.UpdatedAt
            };
        }

        //public async Task<List<TutorStudentDto>> GetStudentsByTutorAsync(string tutorUserId)
        //{
        //    var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
        //    if (tutorProfileId == null)
        //        throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

        //    // Lấy tất cả ClassAssign thuộc về các lớp của Tutor này
        //    // Điều kiện: Lớp của Tutor && Học sinh đã được Approved
        //    var assigns = await _uow.ClassAssigns.GetAllAsync(
        //        filter: ca => ca.Class != null &&
        //                      ca.Class.TutorId == tutorProfileId &&
        //                      ca.ApprovalStatus == ApprovalStatus.Approved,
        //        includes: q => q.Include(ca => ca.Class)
        //                        .Include(ca => ca.Student).ThenInclude(s => s!.User)
        //    );

        //    return assigns.Select(ca => new TutorStudentDto
        //    {
        //        StudentId = ca.StudentId!,
        //        StudentUserId = ca.Student?.UserId ?? "",
        //        StudentName = ca.Student?.User?.UserName ?? "N/A",
        //        StudentEmail = ca.Student?.User?.Email,
        //        StudentPhone = ca.Student?.User?.Phone,
        //        StudentAvatarUrl = ca.Student?.User?.AvatarUrl,

        //        ClassId = ca.ClassId!,
        //        ClassTitle = ca.Class?.Title ?? "N/A",
        //        StudentLimit = ca.Class?.StudentLimit ?? 0,
        //        JoinedAt = ca.EnrolledAt ?? ca.CreatedAt
        //    }).ToList();
        //}

        // Filter students by tutor and class
        public async Task<List<RelatedResourceDto>> GetMyTutorsAsync(string studentUserId)
        {
            // take student profile id
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);

            // Check null
            if (studentProfileId == null) return new List<RelatedResourceDto>();

            var tutors = await _context.ClassAssigns
                .Include(ca => ca.Class)
                    .ThenInclude(c => c.Tutor)
                        .ThenInclude(t => t.User)
                .Where(ca => ca.StudentId == studentProfileId
                             && ca.Class.Status != ClassStatus.Cancelled)
                .Select(ca => ca.Class.Tutor)
                .Distinct()
                .ToListAsync();

            return tutors.Select(t => new RelatedResourceDto
            {
                ProfileId = t.Id.ToString(), // TutorId thường là chuỗi
                UserId = t.UserId,
                FullName = t.User?.UserName ?? t.User?.UserName ?? "N/A",
                AvatarUrl = t.User?.AvatarUrl,
                Email = t.User?.Email,
                Phone = t.User?.Phone
            }).ToList();
        }

        public async Task<List<RelatedResourceDto>> GetMyStudentsAsync(string tutorUserId)
        {
            // SỬA LỖI 2: Gọi đúng tên hàm trong Interface
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);

            // SỬA LỖI 1: tutorProfileId là string?, check null trực tiếp
            if (string.IsNullOrEmpty(tutorProfileId)) return new List<RelatedResourceDto>();

            var students = await _context.ClassAssigns
                .Include(ca => ca.Class)
                .Include(ca => ca.Student)
                    .ThenInclude(s => s.User)
                // SỬA LỖI 1: So sánh trực tiếp, KHÔNG dùng .Value
                .Where(ca => ca.Class.TutorId == tutorProfileId
                             && ca.Class.Status != ClassStatus.Cancelled)
                .Select(ca => ca.Student)
                .Distinct()
                .ToListAsync();

            return students.Select(s => new RelatedResourceDto
            {
                ProfileId = s.Id.ToString(),
                UserId = s.UserId,
                FullName = s.User?.UserName ?? s.User?.UserName ?? "N/A",
                AvatarUrl = s.User?.AvatarUrl,
                Email = s.User?.Email,
                Phone = s.User?.Phone
            }).ToList();
        }

        // --- Helper ---
        private ClassDto MapToClassDto(Class cls)
        {
            return new ClassDto
            {
                Id = cls.Id,
                TutorId = cls.TutorId,
                Title = cls.Title,
                Description = cls.Description,
                Subject = cls.Subject,
                EducationLevel = cls.EducationLevel,
                Price = cls.Price ?? 0,
                Status = cls.Status ?? ClassStatus.Pending,
                CreatedAt = cls.CreatedAt,
                UpdatedAt = cls.UpdatedAt,
                Location = cls.Location,
                CurrentStudentCount = cls.CurrentStudentCount,
                StudentLimit = cls.StudentLimit,
                Mode = cls.Mode.ToString(),
                ClassStartDate = cls.ClassStartDate,
                OnlineStudyLink = cls.OnlineStudyLink,
                // non mapped ScheduleRules
            };
        }
    }
}