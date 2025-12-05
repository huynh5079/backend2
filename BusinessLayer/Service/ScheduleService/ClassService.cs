using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class ClassService : IClassService
    {
        private readonly IScheduleUnitOfWork _uow;
        private readonly IUnitOfWork _mainUow;
        private readonly TpeduContext _context;
        private readonly ITutorProfileService _tutorProfileService;
        private readonly IEscrowService _escrowService;
        private readonly INotificationService _notificationService;
        private readonly SystemWalletOptions _systemWalletOptions;

        public ClassService(
            IScheduleUnitOfWork uow,
            IUnitOfWork mainUow,
            TpeduContext context,
            ITutorProfileService tutorProfileService,
            IEscrowService escrowService,
            INotificationService notificationService,
            IOptions<SystemWalletOptions> systemWalletOptions)
        {
            _uow = uow;
            _mainUow = mainUow;
            _context = context;
            _tutorProfileService = tutorProfileService;
            _escrowService = escrowService;
            _notificationService = notificationService;
            _systemWalletOptions = systemWalletOptions.Value;
        }

        #region Public (Student/Guest)

        public async Task<ClassDto?> GetClassByIdAsync(string classId)
        {
            var targetClass = await _uow.Classes.GetAsync(
                filter: c => c.Id == classId,
                includes: q => q.Include(c => c.ClassSchedules)
                                .Include(c => c.Tutor).ThenInclude(t => t.User)
            );

            if (targetClass == null)
                throw new KeyNotFoundException($"Không tìm thấy lớp học với ID '{classId}'.");

            // include ClassSchedules to MapToClassDto
            return MapToClassDto(targetClass);
        }

        public async Task<IEnumerable<ClassDto>> GetAvailableClassesAsync()
        {
            var classes = await _uow.Classes.GetAllAsync(
                filter: c => (c.Status == ClassStatus.Pending || c.Status == ClassStatus.Active)
                             && c.DeletedAt == null,
                includes: q => q.Include(c => c.ClassSchedules)
                                .Include(c => c.Tutor).ThenInclude(t => t.User)
            );
            // use MapToClassDto to delegate mapping
            return classes
                .OrderByDescending(c => c.CreatedAt)
                .Select(cls => MapToClassDto(cls));
        }

        public async Task<PaginationResult<ClassDto>> SearchAndFilterAvailableAsync(ClassSearchFilterDto filter)
        {
            var rs = await _uow.Classes.SearchAndFilterAvailableAsync(
                filter.Keyword,
                filter.Subject,
                filter.EducationLevel,
                filter.Mode,
                filter.Area,
                filter.MinPrice,
                filter.MaxPrice,
                filter.Status,
                filter.Page,
                filter.PageSize);

            var mapped = rs.Data.Select(cls => MapToClassDto(cls)).ToList();
            return new PaginationResult<ClassDto>(mapped, rs.TotalCount, rs.PageNumber, rs.PageSize);
        }

        #endregion

        #region Tutor Actions

        public async Task<ClassDto> CreateRecurringClassScheduleAsync(string tutorId, CreateClassDto createDto)
        {
            // Validation
            foreach (var rule in createDto.ScheduleRules)
            {
                if (rule.EndTime <= rule.StartTime)
                {
                    throw new InvalidOperationException($"Lịch học {rule.DayOfWeek} có giờ kết thúc sớm hơn giờ bắt đầu.");
                }
            }

            // Kiểm tra duplicate class - Phòng chống trùng lặp
            await CheckForDuplicateClassAsync(tutorId, createDto);

            var newClass = new Class(); // create outside transaction for return purpose
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    newClass = new Class
                    {
                        Id = Guid.NewGuid().ToString(),
                        TutorId = tutorId,
                        Title = createDto.Title,
                        Description = createDto.Description,
                        Price = createDto.Price,
                        Subject = createDto.Subject,
                        EducationLevel = createDto.EducationLevel,
                        Location = createDto.Location,
                        Mode = createDto.Mode, // Enum
                        StudentLimit = createDto.StudentLimit,
                        ClassStartDate = createDto.ClassStartDate,
                        OnlineStudyLink = createDto.OnlineStudyLink,
                        Status = ClassStatus.Pending, // wait student enroll
                        CurrentStudentCount = 0
                    };

                    await _uow.Classes.CreateAsync(newClass);

                    // create ClassSchedules from DTO
                    var newSchedules = createDto.ScheduleRules.Select(ruleDto => new ClassSchedule
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = newClass.Id,
                        DayOfWeek = (byte)ruleDto.DayOfWeek, // Enum -> byte
                        StartTime = ruleDto.StartTime,       // TimeSpan
                        EndTime = ruleDto.EndTime          // TimeSpan
                    }).ToList();

                    // use _context to add range
                    await _context.ClassSchedules.AddRangeAsync(newSchedules);

                    // save all
                    await _uow.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            // newClass.ClassSchedules might be null here,
            // so we pass createDto.ScheduleRules to MapToClassDto
            return MapToClassDto(newClass, createDto.ScheduleRules);
        }

        public async Task<IEnumerable<ClassDto>> GetMyClassesAsync(string tutorUserId)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            var classes = await _uow.Classes.GetAllAsync(
                filter: c => c.TutorId == tutorProfileId && c.DeletedAt == null,
                includes: q => q.Include(c => c.ClassSchedules)
                                .Include(c => c.Tutor).ThenInclude(t => t.User)
            );
            return classes
                .OrderByDescending(c => c.CreatedAt)
                .Select(cls => MapToClassDto(cls));
        }

        public async Task<ClassDto?> UpdateClassAsync(string tutorUserId, string classId, UpdateClassDto dto)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            var targetClass = await _uow.Classes.GetAsync(
                filter: c => c.Id == classId && c.TutorId == tutorProfileId,
                includes: q => q.Include(c => c.ClassSchedules) // IncludeSchedules to MapToClassDto
            );

            if (targetClass == null)
                throw new KeyNotFoundException("Không tìm thấy lớp học hoặc bạn không có quyền sửa.");

            // only allow update when class is still Pending (no student enrolled)
            if (targetClass.Status != ClassStatus.Pending)
                throw new InvalidOperationException($"Không thể sửa lớp học đang ở trạng thái '{targetClass.Status}'.");

            // update fields
            targetClass.Title = dto.Title;
            targetClass.Description = dto.Description;
            targetClass.Price = dto.Price;
            targetClass.Location = dto.Location;
            targetClass.StudentLimit = dto.StudentLimit;
            targetClass.OnlineStudyLink = dto.OnlineStudyLink;
            if (dto.Mode.HasValue)
            {
                targetClass.Mode = dto.Mode.Value;
            }

            await _uow.Classes.UpdateAsync(targetClass);
            await _uow.SaveChangesAsync();

            return MapToClassDto(targetClass);
        }

        public async Task<bool> UpdateClassScheduleAsync(string tutorUserId, string classId, UpdateClassScheduleDto dto)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            // Transaction: delete old schedules, add new schedules
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var targetClass = await _uow.Classes.GetAsync(
                        filter: c => c.Id == classId && c.TutorId == tutorProfileId);

                    if (targetClass == null)
                        throw new KeyNotFoundException("Không tìm thấy lớp học hoặc bạn không có quyền sửa.");

                    // Important: only allow update when class is still Pending (no student enrolled)
                    if (targetClass.Status != ClassStatus.Pending)
                        throw new InvalidOperationException($"Không thể sửa lịch của lớp học đang ở trạng thái '{targetClass.Status}'.");

                    // delete old schedules
                    var oldSchedules = await _context.ClassSchedules
                        .Where(crs => crs.ClassId == classId)
                        .ToListAsync();
                    _context.ClassSchedules.RemoveRange(oldSchedules);

                    // add new schedules from dto
                    var newSchedules = dto.ScheduleRules.Select(s => new ClassSchedule
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = classId,
                        DayOfWeek = (byte)s.DayOfWeek,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime
                    }).ToList();
                    await _context.ClassSchedules.AddRangeAsync(newSchedules);

                    // save all
                    await _uow.SaveChangesAsync();
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

        public async Task<bool> DeleteClassAsync(string tutorUserId, string classId)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            // Transaction delete ClassSchedules and Class
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var targetClass = await _uow.Classes.GetAsync(
                        filter: c => c.Id == classId && c.TutorId == tutorProfileId
                    );

                    if (targetClass == null)
                        throw new KeyNotFoundException("Không tìm thấy lớp học hoặc bạn không có quyền xóa.");

                    if (targetClass.Status != ClassStatus.Pending)
                        throw new InvalidOperationException($"Không thể xóa lớp học đang ở trạng thái '{targetClass.Status}'.");

                    // delete ClassSchedules
                    var schedules = await _context.ClassSchedules.Where(cs => cs.ClassId == classId).ToListAsync();
                    _context.ClassSchedules.RemoveRange(schedules);

                    // cdelete Class
                    // Important: use _context to ensure tracking
                    _context.Classes.Remove(targetClass);

                    // 3. Save
                    await _uow.SaveChangesAsync(); // call uow save changes
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

        public async Task<bool> CompleteClassAsync(string tutorUserId, string classId)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            var targetClass = await _uow.Classes.GetAsync(
                filter: c => c.Id == classId && c.TutorId == tutorProfileId);

            if (targetClass == null)
                throw new KeyNotFoundException("Không tìm thấy lớp học hoặc bạn không có quyền.");

            if (targetClass.Status == ClassStatus.Completed)
                throw new InvalidOperationException("Lớp học đã được đánh dấu hoàn thành rồi.");

            if (targetClass.Status != ClassStatus.Ongoing)
                throw new InvalidOperationException($"Chỉ có thể hoàn thành lớp học đang ở trạng thái Ongoing.");

            // Kiểm tra số buổi đã hoàn thành ≥ 80%
            var completedLessons = await _context.Lessons
                .Where(l => l.ClassId == classId && l.Status == LessonStatus.COMPLETED)
                .CountAsync();
            
            var totalLessons = await _context.Lessons
                .Where(l => l.ClassId == classId)
                .CountAsync();

            if (totalLessons == 0)
                throw new InvalidOperationException("Lớp học chưa có buổi học nào. Không thể hoàn thành.");

            decimal completionPercentage = (decimal)completedLessons / totalLessons;
            const decimal REQUIRED_COMPLETION_PERCENTAGE = 0.8m; // 80%

            if (completionPercentage < REQUIRED_COMPLETION_PERCENTAGE)
            {
                throw new InvalidOperationException(
                    $"Không thể hoàn thành lớp học. Yêu cầu hoàn thành ít nhất {REQUIRED_COMPLETION_PERCENTAGE * 100}% số buổi học. " +
                    $"Hiện tại: {completedLessons}/{totalLessons} buổi ({completionPercentage * 100:F1}%).");
            }

            // Kiểm tra duplicate prevention - Đảm bảo escrow chưa được release
            // Tìm tất cả Escrow của lớp này (với lớp group có nhiều escrow)
            var escrows = await _mainUow.Escrows.GetAllAsync(
                filter: e => e.ClassId == classId && e.Status == EscrowStatus.Held);

            if (!escrows.Any())
                throw new InvalidOperationException("Không tìm thấy escrow cho lớp học này. Vui lòng kiểm tra lại.");

            // Kiểm tra xem có escrow nào đã được release chưa (duplicate prevention)
            var alreadyReleasedEscrows = await _mainUow.Escrows.GetAllAsync(
                filter: e => e.ClassId == classId && 
                           (e.Status == EscrowStatus.Released || e.Status == EscrowStatus.PartiallyReleased));

            if (alreadyReleasedEscrows.Any())
            {
                throw new InvalidOperationException(
                    "Không thể hoàn thành lớp học. Một số escrow đã được giải ngân trước đó. " +
                    "Vui lòng liên hệ admin để kiểm tra.");
            }

            // Cập nhật trạng thái lớp
            targetClass.Status = ClassStatus.Completed;
            await _uow.Classes.UpdateAsync(targetClass);
            await _uow.SaveChangesAsync();

            // Giải ngân tất cả escrow: Hoàn cọc (1 lần) + Giải ngân học phí (từng escrow) + Commission
            // Lưu ý: Deposit chỉ hoàn 1 lần cho cả lớp, không phải từng escrow
            bool depositRefunded = false;
            var errors = new List<string>();

            foreach (var escrow in escrows)
            {
                var releaseResult = await _escrowService.ReleaseAsync(tutorUserId, new ReleaseEscrowRequest { EscrowId = escrow.Id });
                
                if (releaseResult.Status == "Fail")
                {
                    errors.Add($"Escrow {escrow.Id}: {releaseResult.Message}");
                }
                else
                {
                    // Đánh dấu đã hoàn cọc (chỉ 1 lần cho cả lớp)
                    depositRefunded = true;
                }
            }
            
            if (errors.Any())
            {
                // Rollback class status nếu có lỗi
                targetClass.Status = ClassStatus.Ongoing;
                await _uow.Classes.UpdateAsync(targetClass);
                await _uow.SaveChangesAsync();
                throw new InvalidOperationException($"Không thể giải ngân một số escrow: {string.Join("; ", errors)}");
            }

            return true;
        }

        /// <summary>
        /// Tutor hủy lớp sớm hoặc bỏ giữa chừng
        /// - Tutor cancel sớm (chưa dạy hoặc mới dạy ít buổi) → Refund full cho HS, Forfeit deposit
        /// - Tutor bỏ giữa chừng (đã dạy một phần) → Partial release cho tutor, refund phần còn lại cho HS, Forfeit deposit
        /// </summary>
        public async Task<CancelClassResponseDto> CancelClassByTutorAsync(string tutorUserId, string classId, string? reason)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            var classEntity = await _uow.Classes.GetByIdAsync(classId);
            if (classEntity == null)
                throw new KeyNotFoundException("Không tìm thấy lớp học.");

            // Kiểm tra quyền sở hữu
            if (classEntity.TutorId != tutorProfileId)
                throw new UnauthorizedAccessException("Bạn không có quyền hủy lớp học này.");

            // Không cho phép hủy nếu đã Completed hoặc Cancelled
            if (classEntity.Status == ClassStatus.Completed)
                throw new InvalidOperationException("Không thể hủy lớp đã hoàn thành.");
            if (classEntity.Status == ClassStatus.Cancelled)
                throw new InvalidOperationException("Lớp học đã bị hủy rồi.");

            // Kiểm tra số buổi đã học
            var completedLessons = await _context.Lessons
                .Where(l => l.ClassId == classId && l.Status == LessonStatus.COMPLETED)
                .CountAsync();
            
            var totalLessons = await _context.Lessons
                .Where(l => l.ClassId == classId)
                .CountAsync();

            bool hasStartedTeaching = completedLessons > 0;
            decimal teachingPercentage = totalLessons > 0 
                ? (decimal)completedLessons / totalLessons 
                : 0;

            // Lấy tất cả escrow của lớp
            var escrows = await _mainUow.Escrows.GetAllAsync(
                filter: e => e.ClassId == classId && 
                           (e.Status == EscrowStatus.Held || e.Status == EscrowStatus.PartiallyReleased));

            // Lấy tutor deposit
            var tutorDeposit = await _mainUow.TutorDepositEscrows.GetByClassIdAsync(classId);

            using var tx = await _mainUow.BeginTransactionAsync();

            int refundedCount = 0;
            decimal totalRefunded = 0;
            bool depositForfeited = false;
            decimal? depositForfeitAmount = null;

            try
            {
                if (!hasStartedTeaching)
                {
                    // Tutor cancel sớm - Chưa dạy buổi nào
                    // → Refund full cho tất cả học sinh, Forfeit deposit về ví admin
                    foreach (var esc in escrows)
                    {
                        if (esc.Status == EscrowStatus.Held)
                        {
                            var refundResult = await _escrowService.RefundAsync(tutorUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                            if (refundResult.Status == "Ok")
                            {
                                refundedCount++;
                                totalRefunded += esc.GrossAmount;
                            }
                        }
                        else if (esc.Status == EscrowStatus.PartiallyReleased)
                        {
                            // Refund phần còn lại
                            decimal remainingPercentage = 1.0m - (esc.ReleasedAmount / esc.GrossAmount);
                            if (remainingPercentage > 0)
                            {
                                var partialRefundResult = await _escrowService.PartialRefundAsync(tutorUserId, new PartialRefundEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    RefundPercentage = remainingPercentage
                                });
                                
                                if (partialRefundResult.Status == "Ok")
                                {
                                    refundedCount++;
                                    totalRefunded += esc.GrossAmount * remainingPercentage;
                                }
                            }
                        }
                    }

                    // Forfeit deposit về ví admin (system)
                    if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                    {
                        var forfeitResult = await _escrowService.ForfeitDepositAsync(tutorUserId, new ForfeitDepositRequest
                        {
                            TutorDepositEscrowId = tutorDeposit.Id,
                            RefundToStudent = false, // Trả về ví admin (system) như đền bù
                            Reason = $"Gia sư hủy lớp sớm (chưa dạy buổi nào): {reason ?? "Không có lý do"}"
                        });
                        depositForfeited = forfeitResult.Status == "Ok";
                        if (depositForfeited)
                        {
                            depositForfeitAmount = tutorDeposit.DepositAmount;
                        }
                    }
                }
                else
                {
                    // Tutor bỏ giữa chừng - Đã dạy một phần
                    // → Partial release cho tutor theo % đã dạy, refund phần còn lại cho HS, Forfeit deposit
                    foreach (var esc in escrows)
                    {
                        if (esc.Status == EscrowStatus.Held || esc.Status == EscrowStatus.PartiallyReleased)
                        {
                            // Release cho tutor theo % đã dạy
                            decimal alreadyReleasedPercentage = esc.Status == EscrowStatus.PartiallyReleased
                                ? esc.ReleasedAmount / esc.GrossAmount
                                : 0;

                            decimal remainingToRelease = teachingPercentage - alreadyReleasedPercentage;
                            if (remainingToRelease > 0)
                            {
                                await _escrowService.PartialReleaseAsync(tutorUserId, new PartialReleaseEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    ReleasePercentage = remainingToRelease
                                });
                            }

                            // Refund phần còn lại cho HS
                            decimal remainingPercentage = 1.0m - teachingPercentage;
                            if (remainingPercentage > 0)
                            {
                                var partialRefundResult = await _escrowService.PartialRefundAsync(tutorUserId, new PartialRefundEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    RefundPercentage = remainingPercentage
                                });
                                
                                if (partialRefundResult.Status == "Ok")
                                {
                                    refundedCount++;
                                    totalRefunded += esc.GrossAmount * remainingPercentage;
                                }
                            }
                        }
                    }

                    // Forfeit deposit về ví admin
                    if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                    {
                        var forfeitResult = await _escrowService.ForfeitDepositAsync(tutorUserId, new ForfeitDepositRequest
                        {
                            TutorDepositEscrowId = tutorDeposit.Id,
                            RefundToStudent = false, // Trả về ví admin
                            Reason = $"Gia sư bỏ giữa chừng (đã dạy {completedLessons}/{totalLessons} buổi): {reason ?? "Không có lý do"}"
                        });
                        depositForfeited = forfeitResult.Status == "Ok";
                        if (depositForfeited)
                        {
                            depositForfeitAmount = tutorDeposit.DepositAmount;
                        }
                    }
                }

                // Cập nhật trạng thái lớp
                classEntity.Status = ClassStatus.Cancelled;
                await _uow.Classes.UpdateAsync(classEntity);

                // Xóa future lessons
                var futureLessons = await _context.Lessons
                    .Where(l => l.ClassId == classId && l.Status == LessonStatus.SCHEDULED)
                    .ToListAsync();

                if (futureLessons.Any())
                {
                    var futureLessonIds = futureLessons.Select(l => l.Id).ToList();
                    var futureEntries = await _context.ScheduleEntries
                        .Where(se => futureLessonIds.Contains(se.LessonId))
                        .ToListAsync();

                    _context.ScheduleEntries.RemoveRange(futureEntries);
                    _context.Lessons.RemoveRange(futureLessons);
                }

                await _uow.SaveChangesAsync();
                await _mainUow.SaveChangesAsync();
                await tx.CommitAsync();

                // Gửi notification cho tất cả học sinh
                var studentUserIds = escrows.Select(e => e.StudentUserId).Distinct().ToList();
                foreach (var studentUserId in studentUserIds)
                {
                    string message = hasStartedTeaching
                        ? $"Gia sư đã hủy lớp học (đã dạy {completedLessons}/{totalLessons} buổi). {reason ?? ""}"
                        : $"Gia sư đã hủy lớp học trước khi bắt đầu. {reason ?? ""}";
                    
                    var studentNotification = await _notificationService.CreateAccountNotificationAsync(
                        studentUserId,
                        NotificationType.ClassCancelled,
                        message,
                        classId);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(studentUserId, studentNotification);
                }

                return new CancelClassResponseDto
                {
                    ClassId = classId,
                    NewStatus = ClassStatus.Cancelled,
                    Reason = ClassCancelReason.TutorFault,
                    RefundedEscrowsCount = refundedCount,
                    TotalRefundedAmount = totalRefunded,
                    DepositRefunded = depositForfeited,
                    DepositRefundAmount = depositForfeitAmount,
                    Message = hasStartedTeaching
                        ? $"Đã hủy lớp. Đã hoàn {refundedCount} escrow, tổng {totalRefunded:N0} VND. Deposit đã bị tịch thu."
                        : $"Đã hủy lớp sớm. Đã hoàn {refundedCount} escrow, tổng {totalRefunded:N0} VND. Deposit đã bị tịch thu."
                };
            }
            catch (Exception)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Admin Actions

        /// <summary>
        /// Admin hủy lớp 
        /// </summary>
        public async Task<CancelClassResponseDto> CancelClassByAdminAsync(string adminUserId, CancelClassRequestDto request)
        {
            var classEntity = await _uow.Classes.GetByIdAsync(request.ClassId);
            if (classEntity == null)
                throw new KeyNotFoundException("Không tìm thấy lớp học.");

            // Không cho phép hủy nếu đã Completed
            if (classEntity.Status == ClassStatus.Completed)
                throw new InvalidOperationException("Không thể hủy lớp đã hoàn thành.");

            // Kiểm tra số buổi đã học
            var completedLessons = await _context.Lessons
                .Where(l => l.ClassId == request.ClassId && l.Status == LessonStatus.COMPLETED)
                .CountAsync();
            
            var totalLessons = await _context.Lessons
                .Where(l => l.ClassId == request.ClassId)
                .CountAsync();

            bool hasStartedTeaching = completedLessons > 0; // Đã có ít nhất 1 buổi completed

            // Kiểm tra ≥80% hoàn thành - Không cho phép hủy nếu đã hoàn thành ≥80% (trừ một số lý do đặc biệt)
            decimal completionPercentage = totalLessons > 0 
                ? (decimal)completedLessons / totalLessons 
                : 0;
            
            bool isEightyPercentOrMoreCompleted = completionPercentage >= 0.8m;
            
            // Nếu đã hoàn thành ≥80%, chỉ cho phép hủy với các lý do đặc biệt
            if (isEightyPercentOrMoreCompleted)
            {
                bool isAllowedReason = request.Reason == ClassCancelReason.SystemError 
                                    || request.Reason == ClassCancelReason.PolicyViolation
                                    || request.Reason == ClassCancelReason.DuplicateClass;
                
                if (!isAllowedReason)
                {
                    throw new InvalidOperationException(
                        $"Không thể hủy lớp học đã hoàn thành {completionPercentage:P0} ({completedLessons}/{totalLessons} buổi). " +
                        "Chỉ có thể hủy với lý do: Lỗi hệ thống, Vi phạm chính sách, hoặc Lớp trùng lặp.");
                }
            }

            // Lấy tất cả escrow của lớp
            var escrows = await _mainUow.Escrows.GetAllAsync(
                filter: e => e.ClassId == request.ClassId && e.Status == EscrowStatus.Held);

            // Lấy tutor deposit
            var tutorDeposit = await _mainUow.TutorDepositEscrows.GetByClassIdAsync(request.ClassId);

            using var tx = await _mainUow.BeginTransactionAsync();

            int refundedCount = 0;
            decimal totalRefunded = 0;
            bool depositRefunded = false;
            decimal? depositRefundAmount = null;

            // Xử lý theo reason
            switch (request.Reason)
            {
                case ClassCancelReason.SystemError:
                case ClassCancelReason.PolicyViolation:
                case ClassCancelReason.DuplicateClass:
                case ClassCancelReason.IncorrectInfo:
                    // Lỗi hệ thống → Refund full
                    // Refund tất cả escrow
                    foreach (var esc in escrows)
                    {
                        var refundResult = await _escrowService.RefundAsync(adminUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                        if (refundResult.Status == "Ok")
                        {
                            refundedCount++;
                            totalRefunded += esc.GrossAmount;
                        }
                    }

                    // Refund deposit nếu có (hoàn 100% cho tutor vì lỗi hệ thống)
                    if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                    {
                        var refundDepositResult = await RefundTutorDepositAsync(adminUserId, tutorDeposit, "Admin hủy lớp do lỗi hệ thống/setup");
                        if (refundDepositResult)
                        {
                            depositRefunded = true;
                            depositRefundAmount = tutorDeposit.DepositAmount;
                        }
                    }
                    break;

                case ClassCancelReason.TutorFault:
                    // Tutor lỗi
                    if (!hasStartedTeaching)
                    {
                        // Chưa dạy → Refund full HS, Forfeit deposit về ví admin
                        foreach (var esc in escrows)
                        {
                            var refundResult = await _escrowService.RefundAsync(adminUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                            if (refundResult.Status == "Ok")
                            {
                                refundedCount++;
                                totalRefunded += esc.GrossAmount;
                            }
                        }

                        // Forfeit deposit - trả về ví admin (system) như đền bù
                        if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                        {
                            var forfeitResult = await _escrowService.ForfeitDepositAsync(adminUserId, new ForfeitDepositRequest
                            {
                                TutorDepositEscrowId = tutorDeposit.Id,
                                RefundToStudent = false, // Trả về ví admin (system), không trả cho HS
                                Reason = $"Admin hủy lớp do lỗi tutor: {request.Note}"
                            });
                            depositRefunded = forfeitResult.Status == "Ok";
                            if (depositRefunded)
                            {
                                depositRefundAmount = tutorDeposit.DepositAmount;
                            }
                        }
                    }
                    else
                    {
                        // Đã dạy một phần → Partial release cho tutor, refund phần còn lại cho HS
                        // Tính % đã dạy
                        decimal teachingPercentage = totalLessons > 0 
                            ? (decimal)completedLessons / totalLessons 
                            : 0;
                        
                        // Release cho tutor theo % đã dạy
                        foreach (var esc in escrows)
                        {
                            if (esc.Status == EscrowStatus.Held || esc.Status == EscrowStatus.PartiallyReleased)
                            {
                                var partialReleaseResult = await _escrowService.PartialReleaseAsync(adminUserId, new PartialReleaseEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    ReleasePercentage = teachingPercentage
                                });
                                
                                if (partialReleaseResult.Status == "Ok")
                                {
                                    // Refund phần còn lại cho HS (100% - % đã dạy)
                                    decimal refundPercentage = 1.0m - teachingPercentage;
                                    if (refundPercentage > 0)
                                    {
                                        var partialRefundResult = await _escrowService.PartialRefundAsync(adminUserId, new PartialRefundEscrowRequest
                                        {
                                            EscrowId = esc.Id,
                                            RefundPercentage = refundPercentage
                                        });
                                        
                                        if (partialRefundResult.Status == "Ok")
                                        {
                                            refundedCount++;
                                            totalRefunded += esc.GrossAmount * refundPercentage;
                                        }
                                    }
                                }
                            }
                        }

                        // Forfeit deposit về ví admin
                        if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                        {
                            var forfeitResult = await _escrowService.ForfeitDepositAsync(adminUserId, new ForfeitDepositRequest
                            {
                                TutorDepositEscrowId = tutorDeposit.Id,
                                RefundToStudent = false, // Trả về ví admin
                                Reason = $"Admin hủy lớp do lỗi tutor (đã dạy {completedLessons}/{totalLessons} buổi): {request.Note}"
                            });
                            depositRefunded = forfeitResult.Status == "Ok";
                            if (depositRefunded)
                            {
                                depositRefundAmount = tutorDeposit.DepositAmount;
                            }
                        }
                    }
                    break;

                case ClassCancelReason.StudentFault:
                    // HS lỗi
                    // Không refund HS, trả tutor theo buổi đã dạy, hoàn deposit
                    if (hasStartedTeaching)
                    {
                        // Đã dạy → Partial release cho tutor theo % đã dạy
                        decimal teachingPercentage = totalLessons > 0 
                            ? (decimal)completedLessons / totalLessons 
                            : 0;
                        
                        foreach (var esc in escrows)
                        {
                            if (esc.Status == EscrowStatus.Held || esc.Status == EscrowStatus.PartiallyReleased)
                            {
                                await _escrowService.PartialReleaseAsync(adminUserId, new PartialReleaseEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    ReleasePercentage = teachingPercentage
                                });
                            }
                        }
                    }

                    // Hoàn deposit cho tutor (tutor không lỗi)
                    if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                    {
                        var refundDepositResult = await RefundTutorDepositAsync(adminUserId, tutorDeposit, $"Admin hủy lớp do lỗi học sinh: {request.Note}");
                        if (refundDepositResult)
                        {
                            depositRefunded = true;
                            depositRefundAmount = tutorDeposit.DepositAmount;
                        }
                    }
                    break;

                case ClassCancelReason.MutualConsent:
                    // Hai bên đồng ý
                    // Trả tutor theo buổi, refund 80% phần còn lại cho HS
                    if (hasStartedTeaching)
                    {
                        // Đã dạy → Partial release cho tutor theo % đã dạy
                        decimal teachingPercentage = totalLessons > 0 
                            ? (decimal)completedLessons / totalLessons 
                            : 0;
                        
                        foreach (var esc in escrows)
                        {
                            if (esc.Status == EscrowStatus.Held || esc.Status == EscrowStatus.PartiallyReleased)
                            {
                                // Release cho tutor theo % đã dạy
                                await _escrowService.PartialReleaseAsync(adminUserId, new PartialReleaseEscrowRequest
                                {
                                    EscrowId = esc.Id,
                                    ReleasePercentage = teachingPercentage
                                });
                                
                                // Refund 80% phần còn lại cho HS
                                decimal remainingPercentage = 1.0m - teachingPercentage;
                                if (remainingPercentage > 0)
                                {
                                    decimal refundPercentage = remainingPercentage * 0.8m; // 80% của phần còn lại
                                    var partialRefundResult = await _escrowService.PartialRefundAsync(adminUserId, new PartialRefundEscrowRequest
                                    {
                                        EscrowId = esc.Id,
                                        RefundPercentage = refundPercentage
                                    });
                                    
                                    if (partialRefundResult.Status == "Ok")
                                    {
                                        refundedCount++;
                                        totalRefunded += esc.GrossAmount * refundPercentage;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Chưa dạy → Refund 80% cho HS, không trả tutor
                        foreach (var esc in escrows)
                        {
                            var partialRefundResult = await _escrowService.PartialRefundAsync(adminUserId, new PartialRefundEscrowRequest
                            {
                                EscrowId = esc.Id,
                                RefundPercentage = 0.8m // 80%
                            });
                            
                            if (partialRefundResult.Status == "Ok")
                            {
                                refundedCount++;
                                totalRefunded += esc.GrossAmount * 0.8m;
                            }
                        }
                    }

                    // Hoàn deposit (thường 100%)
                    if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                    {
                        var refundDepositResult = await RefundTutorDepositAsync(adminUserId, tutorDeposit, $"Admin hủy lớp theo thỏa thuận: {request.Note}");
                        if (refundDepositResult)
                        {
                            depositRefunded = true;
                            depositRefundAmount = tutorDeposit.DepositAmount;
                        }
                    }
                    break;

                case ClassCancelReason.Other:
                    // Lý do khác → Xử lý như SystemError (refund full để đảm bảo công bằng)
                    // Refund tất cả escrow
                    foreach (var esc in escrows)
                    {
                        var refundResult = await _escrowService.RefundAsync(adminUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                        if (refundResult.Status == "Ok")
                        {
                            refundedCount++;
                            totalRefunded += esc.GrossAmount;
                        }
                    }

                    // Refund deposit nếu có (hoàn 100% cho tutor)
                    if (tutorDeposit != null && tutorDeposit.Status == TutorDepositStatus.Held)
                    {
                        var refundDepositResult = await RefundTutorDepositAsync(adminUserId, tutorDeposit, $"Admin hủy lớp - Lý do khác: {request.Note}");
                        if (refundDepositResult)
                        {
                            depositRefunded = true;
                            depositRefundAmount = tutorDeposit.DepositAmount;
                        }
                    }
                    break;
            }

            // Cập nhật trạng thái lớp
            classEntity.Status = ClassStatus.Cancelled;
            await _uow.Classes.UpdateAsync(classEntity);

            // Xóa future lessons
            var futureLessons = await _context.Lessons
                .Where(l => l.ClassId == request.ClassId && l.Status == LessonStatus.SCHEDULED)
                .ToListAsync();

            if (futureLessons.Any())
            {
                var futureLessonIds = futureLessons.Select(l => l.Id).ToList();
                var futureEntries = await _context.ScheduleEntries
                    .Where(se => futureLessonIds.Contains(se.LessonId))
                    .ToListAsync();

                _context.ScheduleEntries.RemoveRange(futureEntries);
                _context.Lessons.RemoveRange(futureLessons);
            }

            // Cập nhật ClassAssign status
            var classAssigns = await _uow.ClassAssigns.GetByClassIdAsync(request.ClassId);
            foreach (var assign in classAssigns)
            {
                // Có thể thêm field CancelledAt nếu cần
            }

            await _uow.SaveChangesAsync();
            await _mainUow.SaveChangesAsync();
            await tx.CommitAsync();

            // Gửi notification cho tutor và tất cả học sinh
            string reasonMessage = GetCancelReasonMessage(request.Reason, request.Note);
            
            if (!string.IsNullOrEmpty(classEntity.TutorId))
            {
                var tutorProfile = await _mainUow.TutorProfiles.GetByIdAsync(classEntity.TutorId);
                if (tutorProfile != null && !string.IsNullOrEmpty(tutorProfile.UserId))
                {
                    var tutorNotification = await _notificationService.CreateAccountNotificationAsync(
                        tutorProfile.UserId,
                        NotificationType.ClassCancelled,
                        reasonMessage,
                        request.ClassId);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(tutorProfile.UserId, tutorNotification);
                }
            }

            // Gửi notification cho tất cả học sinh
            var studentUserIds = escrows.Select(e => e.StudentUserId).Distinct().ToList();
            foreach (var studentUserId in studentUserIds)
            {
                var studentNotification = await _notificationService.CreateAccountNotificationAsync(
                    studentUserId,
                        NotificationType.ClassCancelled,
                        reasonMessage,
                        request.ClassId);
                await _uow.SaveChangesAsync();
                await _notificationService.SendRealTimeNotificationAsync(studentUserId, studentNotification);
            }

            return new CancelClassResponseDto
            {
                ClassId = request.ClassId,
                NewStatus = ClassStatus.Cancelled,
                Reason = request.Reason,
                RefundedEscrowsCount = refundedCount,
                TotalRefundedAmount = totalRefunded,
                DepositRefunded = depositRefunded,
                DepositRefundAmount = depositRefundAmount,
                Message = $"Đã hủy lớp. Đã hoàn {refundedCount} escrow, tổng {totalRefunded:N0} VND."
            };
        }

        /// <summary>
        /// Admin hủy 1 học sinh khỏi lớp group
        /// </summary>
        /// <summary>
        /// Admin hủy 1 học sinh khỏi lớp (group class)
        /// </summary>
        public async Task<CancelClassResponseDto> CancelStudentEnrollmentAsync(string adminUserId, string classId, string studentId, ClassCancelReason reason, string? note)
        {
            using var tx = await _mainUow.BeginTransactionAsync();
            int refundedCount = 0;
            decimal totalRefunded = 0;

            try
            {
                var classEntity = await _uow.Classes.GetByIdAsync(classId);
                if (classEntity == null)
                    throw new KeyNotFoundException("Không tìm thấy lớp học.");

                if (classEntity.StudentLimit == 1)
                    throw new InvalidOperationException("Không thể hủy học sinh khỏi lớp 1-1. Hãy sử dụng CancelClassByAdminAsync.");

                // Lấy ClassAssign
                var classAssign = await _mainUow.ClassAssigns.GetByClassAndStudentAsync(classId, studentId);
                if (classAssign == null)
                    throw new KeyNotFoundException("Học sinh chưa ghi danh vào lớp học này.");

                // Lấy Escrow của học sinh này
                var escrows = await _mainUow.Escrows.GetAllAsync(
                    filter: e => e.ClassAssignId == classAssign.Id && 
                                 (e.Status == EscrowStatus.Held || e.Status == EscrowStatus.PartiallyReleased));

                // Refund escrow cho học sinh
                foreach (var esc in escrows)
                {
                    if (esc.Status == EscrowStatus.Held)
                    {
                        // Refund full
                        var refundResult = await _escrowService.RefundAsync(adminUserId, new RefundEscrowRequest { EscrowId = esc.Id });
                        if (refundResult.Status == "Ok")
                        {
                            refundedCount++;
                            totalRefunded += esc.GrossAmount;
                        }
                    }
                    else if (esc.Status == EscrowStatus.PartiallyReleased)
                    {
                        // Refund phần còn lại
                        decimal remainingPercentage = 1.0m - (esc.ReleasedAmount / esc.GrossAmount);
                        if (remainingPercentage > 0)
                        {
                            var partialRefundResult = await _escrowService.PartialRefundAsync(adminUserId, new PartialRefundEscrowRequest
                            {
                                EscrowId = esc.Id,
                                RefundPercentage = remainingPercentage
                            });
                            
                            if (partialRefundResult.Status == "Ok")
                            {
                                refundedCount++;
                                totalRefunded += esc.GrossAmount * remainingPercentage;
                            }
                        }
                    }
                }

                // Cập nhật ClassAssign
                classAssign.PaymentStatus = PaymentStatus.Refunded;
                classAssign.ApprovalStatus = ApprovalStatus.Pending; // Hoặc có thể thêm Rejected vào enum nếu cần
                await _mainUow.ClassAssigns.UpdateAsync(classAssign);

                // Giảm CurrentStudentCount
                if (classEntity.CurrentStudentCount > 0)
                {
                    classEntity.CurrentStudentCount--;
                }
                await _uow.Classes.UpdateAsync(classEntity);

                await _uow.SaveChangesAsync();
                await _mainUow.SaveChangesAsync();
                await tx.CommitAsync();

                // Gửi notification cho học sinh
                var studentProfile = await _mainUow.StudentProfiles.GetByIdAsync(studentId);
                if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId))
                {
                    string reasonMessage = GetCancelReasonMessage(reason, note);
                    var studentNotification = await _notificationService.CreateAccountNotificationAsync(
                        studentProfile.UserId,
                        NotificationType.ClassCancelled,
                        $"Bạn đã bị hủy khỏi lớp học. {reasonMessage}",
                        classId);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, studentNotification);
                }

                return new CancelClassResponseDto
                {
                    ClassId = classId,
                    NewStatus = classEntity.Status ?? ClassStatus.Cancelled,
                    Reason = reason,
                    RefundedEscrowsCount = refundedCount,
                    TotalRefundedAmount = totalRefunded,
                    Message = $"Đã hủy học sinh khỏi lớp. Đã hoàn {refundedCount} escrow, tổng {totalRefunded:N0} VND."
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Helper: Refund tutor deposit
        /// </summary>
        private async Task<bool> RefundTutorDepositAsync(string adminUserId, TutorDepositEscrow deposit, string reason)
        {
            if (deposit.Status != TutorDepositStatus.Held)
                return false;

            var adminWallet = await _mainUow.Wallets.GetByUserIdAsync(_systemWalletOptions.SystemWalletUserId);
            if (adminWallet == null)
            {
                // Tạo wallet nếu chưa có
                adminWallet = new Wallet
                {
                    UserId = _systemWalletOptions.SystemWalletUserId,
                    Balance = 0,
                    Currency = "VND",
                    IsFrozen = false
                };
                await _mainUow.Wallets.AddAsync(adminWallet);
                await _mainUow.SaveChangesAsync();
            }

            // Lấy tutor wallet trực tiếp từ TutorUserId (không cần TutorProfile)
            var tutorWallet = await _mainUow.Wallets.GetByUserIdAsync(deposit.TutorUserId);
            if (tutorWallet == null)
            {
                // Tạo wallet nếu chưa có
                tutorWallet = new Wallet
                {
                    UserId = deposit.TutorUserId,
                    Balance = 0,
                    Currency = "VND",
                    IsFrozen = false
                };
                await _mainUow.Wallets.AddAsync(tutorWallet);
            }

            if (adminWallet.Balance < deposit.DepositAmount)
                return false;

            adminWallet.Balance -= deposit.DepositAmount;
            tutorWallet.Balance += deposit.DepositAmount;

            await _mainUow.Wallets.Update(adminWallet);
            await _mainUow.Wallets.Update(tutorWallet);

            await _mainUow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.DepositRefundOut,
                Status = TransactionStatus.Succeeded,
                Amount = -deposit.DepositAmount,
                Note = $"Hoàn cọc cho tutor: {reason}",
                CounterpartyUserId = deposit.TutorUserId
            });

            await _mainUow.Transactions.AddAsync(new Transaction
            {
                WalletId = tutorWallet.Id,
                Type = TransactionType.DepositRefundIn,
                Status = TransactionStatus.Succeeded,
                Amount = deposit.DepositAmount,
                Note = $"Nhận hoàn cọc: {reason}",
                CounterpartyUserId = adminUserId
            });

            deposit.Status = TutorDepositStatus.Refunded;
            deposit.RefundedAt = DateTime.UtcNow;
            await _mainUow.TutorDepositEscrows.UpdateAsync(deposit);

            await _mainUow.SaveChangesAsync();

            // Gửi notification cho tutor khi deposit được refund
            if (!string.IsNullOrEmpty(deposit.TutorUserId))
            {
                try
                {
                    var notification = await _notificationService.CreateWalletNotificationAsync(
                        deposit.TutorUserId,
                        NotificationType.TutorDepositRefunded,
                        deposit.DepositAmount,
                        $"Tiền cọc đã được hoàn lại: {reason}",
                        deposit.Id);
                    await _mainUow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(deposit.TutorUserId, notification);
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                }
            }

            return true;
        }

        /// <summary>
        /// Helper: Lấy message lý do hủy lớp
        /// </summary>
        private string GetCancelReasonMessage(ClassCancelReason reason, string? note)
        {
            var baseMessage = reason switch
            {
                ClassCancelReason.SystemError => "Lớp học đã bị hủy do lỗi hệ thống/setup.",
                ClassCancelReason.TutorFault => "Lớp học đã bị hủy do lỗi từ phía gia sư.",
                ClassCancelReason.StudentFault => "Lớp học đã bị hủy do lỗi từ phía học sinh.",
                ClassCancelReason.MutualConsent => "Lớp học đã được hủy theo thỏa thuận giữa hai bên.",
                ClassCancelReason.PolicyViolation => "Lớp học đã bị hủy do vi phạm chính sách.",
                ClassCancelReason.DuplicateClass => "Lớp học đã bị hủy do trùng lặp.",
                ClassCancelReason.IncorrectInfo => "Lớp học đã bị hủy do thông tin không chính xác.",
                ClassCancelReason.Other => "Lớp học đã bị hủy do lý do khác.",
                _ => "Lớp học đã bị hủy."
            };

            if (!string.IsNullOrWhiteSpace(note))
            {
                return $"{baseMessage} Lý do: {note}";
            }

            return baseMessage;
        }

        #endregion

        #region Helper

        /// <summary>
        /// Kiểm tra duplicate class - Phòng chống trùng lặp
        /// Kiểm tra xem có lớp học tương tự đã tồn tại với:
        /// - Cùng gia sư
        /// - Cùng môn học
        /// - Cùng cấp độ
        /// - Cùng mode (Online/Offline)
        /// - Lịch học trùng lặp (cùng ngày và thời gian chồng chéo)
        /// - Giá tương tự (trong khoảng ±10%)
        /// </summary>
        private async Task CheckForDuplicateClassAsync(string tutorId, CreateClassDto createDto)
        {
            await CheckForDuplicateClassAsync(
                tutorId,
                createDto.Subject,
                createDto.EducationLevel,
                createDto.Mode,
                createDto.Price,
                createDto.ScheduleRules.Select(r => new { r.DayOfWeek, r.StartTime, r.EndTime }).ToList()
            );
        }

        /// <summary>
        /// Overload method để kiểm tra duplicate với các tham số trực tiếp
        /// </summary>
        private async Task CheckForDuplicateClassAsync(
            string tutorId,
            string subject,
            string educationLevel,
            ClassMode mode,
            decimal price,
            IEnumerable<dynamic> scheduleRules)
        {
            // Tìm các lớp học của cùng gia sư với cùng môn học, cấp độ và mode
            var existingClasses = await _uow.Classes.GetAllAsync(
                filter: c => c.TutorId == tutorId
                           && c.Subject == subject
                           && c.EducationLevel == educationLevel
                           && c.Mode == mode
                           && c.DeletedAt == null
                           && (c.Status == ClassStatus.Pending || c.Status == ClassStatus.Active || c.Status == ClassStatus.Ongoing),
                includes: q => q.Include(c => c.ClassSchedules)
            );

            if (!existingClasses.Any())
                return; // Không có lớp nào tương tự

            // Kiểm tra giá tương tự (trong khoảng ±10%)
            decimal priceTolerance = price * 0.1m;
            var similarPriceClasses = existingClasses
                .Where(c => c.Price.HasValue && Math.Abs(c.Price.Value - price) <= priceTolerance)
                .ToList();

            if (!similarPriceClasses.Any())
                return; // Không có lớp nào có giá tương tự

            // Kiểm tra lịch học trùng lặp
            foreach (var existingClass in similarPriceClasses)
            {
                if (existingClass.ClassSchedules == null || !existingClass.ClassSchedules.Any())
                    continue;

                // So sánh từng lịch học trong lớp mới với các lịch học trong lớp hiện có
                foreach (var newSchedule in scheduleRules)
                {
                    foreach (var existingSchedule in existingClass.ClassSchedules)
                    {
                        // Kiểm tra cùng ngày trong tuần
                        if ((DayOfWeek)existingSchedule.DayOfWeek == newSchedule.DayOfWeek)
                        {
                            // Kiểm tra thời gian chồng chéo
                            // Hai khoảng thời gian chồng chéo nếu: start1 < end2 && start2 < end1
                            TimeSpan newStart = newSchedule.StartTime;
                            TimeSpan newEnd = newSchedule.EndTime;
                            TimeSpan existingStart = existingSchedule.StartTime;
                            TimeSpan existingEnd = existingSchedule.EndTime;

                            if (newStart < existingEnd && existingStart < newEnd)
                            {
                                throw new InvalidOperationException(
                                    $"Đã tồn tại lớp học tương tự (ID: {existingClass.Id}, Tiêu đề: {existingClass.Title}) " +
                                    $"với cùng môn học, cấp độ, mode và lịch học trùng lặp vào {newSchedule.DayOfWeek} " +
                                    $"từ {newStart:hh\\:mm} đến {newEnd:hh\\:mm}. " +
                                    "Vui lòng kiểm tra lại hoặc hủy lớp học trùng lặp trước khi tạo lớp mới.");
                            }
                        }
                    }
                }
            }
        }

        private ClassDto MapToClassDto(Class cls, List<RecurringScheduleRuleDto> rules = null)
        {
            // cls must include ClassSchedules to map
            var scheduleRules = rules ?? cls.ClassSchedules?.Select(r => new RecurringScheduleRuleDto
            {
                DayOfWeek = (DayOfWeek)r.DayOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime
            }).ToList();

            return new ClassDto
            {
                Id = cls.Id,
                // Tutor identity fields
                TutorId = cls.TutorId,
                TutorUserId = cls.Tutor?.UserId,
                TutorName = cls.Tutor?.User?.UserName ?? cls.Tutor?.User?.UserName ?? "N/A",
                // other class fields
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
                Mode = cls.Mode.ToString(), // Enum -> String
                ClassStartDate = cls.ClassStartDate,
                OnlineStudyLink = cls.OnlineStudyLink,
                // return schedule rules
                ScheduleRules = scheduleRules
            };
        }

        #endregion
    }
}