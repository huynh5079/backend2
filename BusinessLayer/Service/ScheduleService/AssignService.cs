using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.DTOs.Schedule.ClassAssign;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using Humanizer;
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
        private readonly IParentProfileRepository _parentRepo;


        public AssignService(
            TpeduContext context,
            IScheduleUnitOfWork uow,
            IUnitOfWork mainUow,
            IStudentProfileService studentProfileService,
            ITutorProfileService tutorProfileService,
            IScheduleGenerationService scheduleGenerationService,
            IEscrowService escrowService,
            INotificationService notificationService,
            IParentProfileRepository parentRepo)
        {
            _context = context;
            _uow = uow;
            _mainUow = mainUow;
            _studentProfileService = studentProfileService;
            _tutorProfileService = tutorProfileService;
            _scheduleGenerationService = scheduleGenerationService;
            _escrowService = escrowService;
            _notificationService = notificationService; 
            _parentRepo = parentRepo;
        }

        #region Student actions
        public async Task<ClassAssignDetailDto> AssignRecurringClassAsync(string actorUserId, string userRole, AssignRecurringClassDto dto)
        {
            // Validate user and resolve target student
            string payerUserId = actorUserId; // The user performing the action and the payer
            string targetStudentId = await ResolveTargetStudentProfileIdAsync(actorUserId, userRole, dto.StudentId);

            // Validate class existence and status
            var classEntity = await _uow.Classes.GetByIdAsync(dto.ClassId);
            if (classEntity == null)
                throw new KeyNotFoundException("Lớp học không tồn tại.");

            if (classEntity.Status != ClassStatus.Pending && classEntity.Status != ClassStatus.Active)
                throw new InvalidOperationException($"Không thể đăng ký lớp đang ở trạng thái '{classEntity.Status}'.");

            if (classEntity.CurrentStudentCount >= classEntity.StudentLimit)
                throw new InvalidOperationException("Lớp học đã đủ số lượng học viên.");

            // Check enrrollment
            var existingAssign = await _uow.ClassAssigns.GetAsync(ca => ca.ClassId == dto.ClassId && ca.StudentId == targetStudentId);
            if (existingAssign != null)
                throw new InvalidOperationException("Học sinh này đã đăng ký lớp học này rồi.");

            // Logic payment from wallet and create ClassAssign within a transaction
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Subract money from payer's wallet
                    // validate wallet balance
                    var payerWallet = await _mainUow.Wallets.GetByUserIdAsync(payerUserId);
                    if (payerWallet == null || payerWallet.Balance < (classEntity.Price ?? 0))
                    {
                        throw new InvalidOperationException($"Số dư ví không đủ để thanh toán. Cần {classEntity.Price:N0} VND.");
                    }

                    // Subtract balance
                    payerWallet.Balance -= (classEntity.Price ?? 0);
                    await _mainUow.Wallets.Update(payerWallet);

                    // record Transaction
                    var paymentTx = new Transaction
                    {
                        WalletId = payerWallet.Id,
                        Type = TransactionType.Debit,
                        Amount = -(classEntity.Price ?? 0),
                        Status = TransactionStatus.Succeeded,
                        CreatedAt = DateTime.Now,
                        //Description = $"Thanh toán học phí lớp {classEntity.Title}" + (userRole == "Parent" ? $" (cho con)" : "")
                    };
                    await _mainUow.Transactions.AddAsync(paymentTx);

                    // create ClassAssign
                    var newAssign = new ClassAssign
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = dto.ClassId,
                        StudentId = targetStudentId,
                        ApprovalStatus = ApprovalStatus.Approved, // Paid enrollment = auto approved
                        PaymentStatus = PaymentStatus.Paid,       // Subtracted from wallet = paid
                        EnrolledAt = DateTime.Now
                    };
                    await _uow.ClassAssigns.CreateAsync(newAssign);

                    // update Class
                    classEntity.CurrentStudentCount++;

                    ////if full, set to Ongoing???
                    //if (classEntity.CurrentStudentCount >= classEntity.StudentLimit)
                    //{
                    //    classEntity.Status = ClassStatus.Ongoing;
                    //}

                    await _uow.Classes.UpdateAsync(classEntity);

                    // Commit Transaction
                    await _uow.SaveChangesAsync();
                    await _mainUow.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    // If any error, rollback transaction
                    await transaction.RollbackAsync();
                    throw;
                }

                try
                {
                // Send Notifications
                // Notify payer about successful payment
                await _notificationService.CreateAccountNotificationAsync(
                        payerUserId,
                        NotificationType.EscrowPaid,
                        $"Thanh toán thành công {classEntity.Price:N0} VND cho lớp {classEntity.Title}.",
                        classEntity.Id);

                    // If parent paid, notify the student as well
                    if (userRole == "Parent")
                    {
                        var studentUser = await _mainUow.StudentProfiles.GetByIdAsync(targetStudentId);
                        if (studentUser?.UserId != null)
                        {
                            await _notificationService.CreateAccountNotificationAsync(
                                studentUser.UserId,
                                NotificationType.ClassEnrollmentSuccess,
                                $"Phụ huynh đã đăng ký thành công lớp {classEntity.Title} cho bạn.",
                                classEntity.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error 
                    Console.WriteLine($"Gửi thông báo thất bại: {ex.Message}");
                }

                // Return Detail DTO
                return await GetEnrollmentDetailAsync(payerUserId, dto.ClassId);

            });
        }

        /// <summary>
        /// [TRANSACTION] Student withdraw from class - Học sinh hủy enrollment
        /// Xử lý refund escrow cho học sinh khi rút khỏi lớp
        /// </summary>
        public async Task<bool> WithdrawFromClassAsync(string actorUserId, string userRole, string classId, string? studentId)
        {
            string targetStudentId = await ResolveTargetStudentProfileIdAsync(actorUserId, userRole, studentId);

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // load ClassAssign
                    var assignment = await _uow.ClassAssigns.GetAsync(
                        a => a.StudentId == targetStudentId && a.ClassId == classId);

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
                            // Refund full for
                            await _escrowService.RefundAsync(actorUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                        }
                        else if (esc.Status == EscrowStatus.PartiallyReleased)
                        {
                            // Đã release một phần cho tutor → Refund phần còn lại (100% của phần còn lại)
                            decimal remainingPercentage = 1.0m - (esc.ReleasedAmount / esc.GrossAmount);
                            if (remainingPercentage > 0)
                            {
                                await _escrowService.PartialRefundAsync(actorUserId, new PartialRefundEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    RefundPercentage = remainingPercentage
                                });
                            }
                        }
                    }

                    // update PaymentStatus for ClassAssign
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

        public async Task<List<MyEnrolledClassesDto>> GetMyEnrolledClassesAsync(string actorUserId, string userRole, string? studentId)
        {
            string targetStudentId = await ResolveTargetStudentProfileIdAsync(actorUserId, userRole, studentId);

            var classAssigns = await _uow.ClassAssigns.GetByStudentIdAsync(targetStudentId, includeClass: true);

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

        public async Task<EnrollmentCheckDto> CheckEnrollmentAsync(string actorUserId, string userRole, string classId, string? studentId)
        {
            string targetStudentId = await ResolveTargetStudentProfileIdAsync(actorUserId, userRole, studentId);

            var isEnrolled = await _uow.ClassAssigns.IsApprovedAsync(classId, targetStudentId);

            return new EnrollmentCheckDto
            {
                ClassId = classId,
                IsEnrolled = isEnrolled
            };
        }

        public async Task<ClassAssignDetailDto> GetEnrollmentDetailAsync(string userId, string classId)
        {
            // Student/Parent can only view their own enrollment
            var assigns = await _uow.ClassAssigns.GetAllAsync(
                filter: ca => ca.ClassId == classId,
                includes: q => q.Include(c => c.Class)
                                .Include(c => c.Student).ThenInclude(s => s.User)
            );

            if (assigns == null || !assigns.Any())
                throw new KeyNotFoundException("Không tìm thấy thông tin đăng ký cho lớp học này.");

            // Filter based on user role:
            // If user is Student: view own enrollment
            // Nếu user is Parent: view enrollment child

            ClassAssign? targetAssign = null;

            // Check if user is Student
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(userId);
            if (studentProfileId != null)
            {
                targetAssign = assigns.FirstOrDefault(ca => ca.StudentId == studentProfileId);
            }
            else
            {
                // if not student, check if Parent
                foreach (var assign in assigns)
                {
                    if (assign.StudentId != null)
                    {
                        var isParent = await _parentRepo.ExistsLinkAsync(userId, assign.StudentId);
                        if (isParent)
                        {
                            targetAssign = assign;
                            break; // find first match
                        }
                    }
                }
            }

            if (targetAssign == null)
                throw new UnauthorizedAccessException("Bạn không có quyền xem thông tin đăng ký này hoặc bạn chưa đăng ký.");

            // 3. Map to DTO
            return new ClassAssignDetailDto
            {
                ClassAssignId = targetAssign.Id,
                ClassId = targetAssign.ClassId ?? string.Empty,
                ClassTitle = targetAssign.Class?.Title ?? "N/A",
                ClassDescription = targetAssign.Class?.Description,
                ClassSubject = targetAssign.Class?.Subject,
                ClassEducationLevel = targetAssign.Class?.EducationLevel,
                ClassPrice = targetAssign.Class?.Price ?? 0,
                ClassStatus = targetAssign.Class?.Status ?? ClassStatus.Pending,

                StudentId = targetAssign.StudentId ?? string.Empty,
                StudentName = targetAssign.Student?.User?.UserName ?? "N/A",
                StudentEmail = targetAssign.Student?.User?.Email,
                StudentPhone = targetAssign.Student?.User?.Phone,
                StudentAvatarUrl = targetAssign.Student?.User?.AvatarUrl,

                ApprovalStatus = targetAssign.ApprovalStatus,
                PaymentStatus = targetAssign.PaymentStatus,
                EnrolledAt = targetAssign.EnrolledAt,
                CreatedAt = targetAssign.CreatedAt,
                UpdatedAt = targetAssign.UpdatedAt
            };
        }
        #endregion

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

        #region Tutor actions
        public async Task<List<RelatedResourceDto>> GetMyTutorsAsync(string actorUserId, string userRole, string? studentId)
        {
            // take student profile id
            string targetStudentId = await ResolveTargetStudentProfileIdAsync(actorUserId, userRole, studentId);

            // Check null
            if (targetStudentId == null) return new List<RelatedResourceDto>();

            var tutors = await _context.ClassAssigns
                .Include(ca => ca.Class)
                    .ThenInclude(c => c.Tutor)
                        .ThenInclude(t => t.User)
                .Where(ca => ca.StudentId == targetStudentId
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

        public async Task<List<RelatedResourceDto>> GetMyStudentsAsync(string tutorUserId)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);

            // tutorProfileId string?, check null
            if (string.IsNullOrEmpty(tutorProfileId)) return new List<RelatedResourceDto>();

            var students = await _context.ClassAssigns
                .Include(ca => ca.Class)
                .Include(ca => ca.Student)
                    .ThenInclude(s => s.User)
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
        #endregion

        // --- Helper ---
        private async Task<string> ResolveTargetStudentProfileIdAsync(string actorUserId, string userRole, string? inputStudentId)
        {
            if (userRole == "Student")
            {
                var profileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(actorUserId);
                if (string.IsNullOrEmpty(profileId))
                    throw new KeyNotFoundException("Không tìm thấy hồ sơ học sinh của bạn.");
                return profileId;
            }
            else if (userRole == "Parent")
            {
                if (string.IsNullOrEmpty(inputStudentId))
                    throw new ArgumentException("Phụ huynh cần chọn học sinh để đăng ký.");

                // Validate Parent-Child Link
                // inputStudentId ở đây là ID của bảng StudentProfile
                var isLinked = await _parentRepo.ExistsLinkAsync(actorUserId, inputStudentId);
                if (!isLinked)
                    throw new UnauthorizedAccessException("Bạn không có quyền đăng ký cho học sinh này.");

                return inputStudentId;
            }
            throw new UnauthorizedAccessException("Role không hợp lệ.");
        }

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