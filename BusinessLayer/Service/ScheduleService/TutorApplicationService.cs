using BusinessLayer.DTOs.Schedule.TutorApplication;
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
    public class TutorApplicationService : ITutorApplicationService
    {
        private readonly IScheduleUnitOfWork _uow;
        private readonly IUnitOfWork _mainUow;
        private readonly TpeduContext _context;
        private readonly ITutorProfileService _tutorProfileService;
        private readonly IStudentProfileService _studentProfileService;
        private readonly IScheduleGenerationService _scheduleGenerationService;
        private readonly INotificationService _notificationService;
        private readonly IParentProfileRepository _parentRepo;

        public TutorApplicationService(
            IScheduleUnitOfWork uow,
            IUnitOfWork mainUow,
            TpeduContext context,
            ITutorProfileService tutorProfileService,
            IStudentProfileService studentProfileService,
            IScheduleGenerationService scheduleGenerationService,
            INotificationService notificationService,
            IParentProfileRepository parentRepo)
        {
            _uow = uow;
            _mainUow = mainUow;
            _context = context;
            _tutorProfileService = tutorProfileService;
            _studentProfileService = studentProfileService;
            _scheduleGenerationService = scheduleGenerationService;
            _notificationService = notificationService;
            _parentRepo = parentRepo;
        }

        #region Tutor's Actions

        public async Task<TutorApplicationResponseDto?> CreateApplicationAsync(string tutorUserId, CreateTutorApplicationDto dto)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            // Check if ClassRequest exists and is Active   
            var classRequest = await _uow.ClassRequests.GetAsync(
                filter: cr => cr.Id == dto.ClassRequestId,
                includes: q => q.Include(cr => cr.Student).ThenInclude(s => s.User));
            if (classRequest == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu lớp học.");
            //if (classRequest.Status != ClassRequestStatus.Pending)
            //    throw new InvalidOperationException($"Không thể ứng tuyển vào yêu cầu đang ở trạng thái '{classRequest.Status}'.");

            // Check if have applied yet
            var existingApplication = await _uow.TutorApplications.GetAsync(
                a => a.TutorId == tutorProfileId && a.ClassRequestId == dto.ClassRequestId);
            if (existingApplication != null)
                throw new InvalidOperationException("Bạn đã ứng tuyển vào yêu cầu này rồi.");

            // create Application
            var newApplication = new TutorApplication
            {
                Id = Guid.NewGuid().ToString(),
                TutorId = tutorProfileId,
                ClassRequestId = dto.ClassRequestId,
                Status = ApplicationStatus.Pending,
                MeetingLink = dto.MeetingLink
            };

            await _uow.TutorApplications.CreateAsync(newApplication); // Unsave
            await _uow.SaveChangesAsync(); // Save

            // Gửi notification cho student khi có gia sư mới apply vào request
            if (classRequest.StudentId != null)
            {
                var studentProfile = await _mainUow.StudentProfiles.GetByIdAsync(classRequest.StudentId);
                if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId))
                {
                    try
                    {
                        // Lấy thông tin tutor để lấy tên
                        var tutorProfile = await _mainUow.TutorProfiles.GetAsync(
                            filter: t => t.Id == tutorProfileId,
                            includes: q => q.Include(t => t.User));
                        var tutorName = tutorProfile?.User?.UserName ?? "một gia sư";
                        var notification = await _notificationService.CreateAccountNotificationAsync(
                            studentProfile.UserId,
                            NotificationType.TutorApplicationReceived,
                            $"{tutorName} đã ứng tuyển vào yêu cầu lớp học của bạn. Vui lòng xem xét và chấp nhận.",
                            dto.ClassRequestId);
                        await _uow.SaveChangesAsync();
                        await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, notification);
                    }
                    catch (Exception notifEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                    }
                }
            }

            return MapToResponseDto(newApplication); // DTO
        }

        public async Task<bool> WithdrawApplicationAsync(string tutorUserId, string applicationId)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            var application = await _uow.TutorApplications.GetAsync(
                a => a.Id == applicationId && a.TutorId == tutorProfileId);

            if (application == null)
                throw new KeyNotFoundException("Không tìm thấy đơn ứng tuyển hoặc bạn không có quyền.");

            if (application.Status != ApplicationStatus.Pending)
                throw new InvalidOperationException("Không thể rút đơn khi đã được xử lý.");

            // Use context to delete (because UoW repository doesn't have Remove function)
            _context.TutorApplications.Remove(application);
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TutorApplicationResponseDto>> GetMyApplicationsAsync(string tutorUserId)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                return Enumerable.Empty<TutorApplicationResponseDto>();

            var applications = await _uow.TutorApplications.GetAllAsync(
                filter: a => a.TutorId == tutorProfileId,
                includes: q => q.Include(a => a.Tutor).ThenInclude(t => t.User) // Include để lấy tên
            );

            return applications
                .OrderByDescending(a => a.CreatedAt)
                .Select(MapToResponseDto);
        }

        #endregion

        #region Student, Parent's Actions

        /// <summary>
        /// [CORE TRANSACTION] Student accepts 1 application from Tutor
        /// </summary>
        public async Task<bool> AcceptApplicationAsync(string actorUserId, string role, string applicationId)
        {
            var app = await _uow.TutorApplications.GetAsync(
                filter: a => a.Id == applicationId,
                includes: q => q.Include(a => a.Tutor)
            );
            if (app == null) throw new KeyNotFoundException("Không tìm thấy đơn ứng tuyển.");

            // Capture request result to use its StudentId
            var validatedRequest = await ValidateRequestOwnershipAsync(actorUserId, role, app.ClassRequestId);

            if (validatedRequest.Status != ClassRequestStatus.Pending)
                throw new InvalidOperationException("Yêu cầu này đã được xử lý hoặc đã đóng.");

            // Start Big Transaction
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Load data (Application, Request, and RequestSchedules)
                    var application = await _uow.TutorApplications.GetAsync(
                        filter: a => a.Id == applicationId,
                        includes: q => q.Include(app => app.ClassRequest)
                                        .ThenInclude(cr => cr.ClassRequestSchedules)
                    );

                    // Validation
                    if (application == null)
                        throw new KeyNotFoundException("Đơn ứng tuyển không tồn tại.");
                    if (application.ClassRequest == null)
                        throw new KeyNotFoundException("Yêu cầu lớp học liên quan không tồn tại.");
                    if (application.ClassRequest.StudentId != validatedRequest.StudentId)
                        throw new UnauthorizedAccessException("Bạn không có quyền chấp nhận đơn này.");
                    if (application.Status != ApplicationStatus.Pending)
                        throw new InvalidOperationException("Đơn này đã được xử lý.");

                    var request = application.ClassRequest;

                    // CALL PAYMENT LOGIC
                    // (Temporarily ignore as agreed)
                    // var paymentResult = await _paymentService.ProcessClassPayment(studentProfileId, request.Budget ?? 0);
                    // if (!paymentResult.IsSuccess)
                    // throw new InvalidOperationException(paymentResult.Message);

                    // Kiểm tra duplicate class trước khi tạo
                    await CheckForDuplicateClassFromRequestAsync(application.TutorId, request);

                    // --- START CREATE NEW CLASS ---

                    // CREATE CLASS
                    var newClass = new Class
                    {
                        Id = Guid.NewGuid().ToString(),
                        TutorId = application.TutorId, // Get TutorId from application
                        Title = $"Lớp {request.Subject} (từ yêu cầu {request.Id})",
                        Description = $"{request.Description}\n\nYêu cầu đặc biệt: {request.SpecialRequirements}",
                        Price = request.Budget,
                        Status = ClassStatus.Pending, // Chờ học sinh thanh toán - sẽ chuyển Ongoing khi đã thanh toán + đặt cọc
                        Location = request.Location,
                        Mode = request.Mode,
                        Subject = request.Subject,
                        EducationLevel = request.EducationLevel,
                        ClassStartDate = request.ClassStartDate,
                        StudentLimit = 1, // 1-1
                        CurrentStudentCount = 1 ,

                        OnlineStudyLink = !string.IsNullOrEmpty(application.MeetingLink)
                                            ? application.MeetingLink
                                            : request.OnlineStudyLink
                    };
                    await _uow.Classes.CreateAsync(newClass); // no save

                    // CLASSASSIGN
                    var newAssignment = new ClassAssign
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = newClass.Id,
                        StudentId = request.StudentId,
                        PaymentStatus = PaymentStatus.Pending, // Chưa thanh toán - sẽ thanh toán qua /escrow/pay
                        ApprovalStatus = ApprovalStatus.Approved, // Tutor đã apply + được HS chọn = Approved
                        EnrolledAt = DateTime.UtcNow
                    };
                    await _uow.ClassAssigns.CreateAsync(newAssignment); // Unsave

                    // Copy schedule from Request to Class
                    var newClassSchedules = request.ClassRequestSchedules.Select(reqSchedule => new ClassSchedule
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = newClass.Id,
                        DayOfWeek = reqSchedule.DayOfWeek ?? 0,
                        StartTime = reqSchedule.StartTime,
                        EndTime = reqSchedule.EndTime
                    }).ToList();

                    await _context.ClassSchedules.AddRangeAsync(newClassSchedules); // Unsave

                    // UPDATE STATUS (Close Request and Application)
                    application.Status = ApplicationStatus.Accepted;
                    request.Status = ClassRequestStatus.Matched; // Matched tutor to prevent more applications

                    await _uow.TutorApplications.UpdateAsync(application); // Unsave
                    await _uow.ClassRequests.UpdateAsync(request); // Unsave

                    // create schedule for class based on request schedule
                    await _scheduleGenerationService.GenerateScheduleFromRequestAsync(
                        newClass.Id,
                        application.TutorId,
                        request.ClassStartDate ?? DateTime.UtcNow,
                        request.ClassRequestSchedules //Use request scheduler
                    );

                    // SAVE ALL
                    await _uow.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    // Send notifications outside transaction
                    var tutorProfile = await _mainUow.TutorProfiles.GetByIdAsync(application.TutorId);
                    if (tutorProfile != null && !string.IsNullOrEmpty(tutorProfile.UserId))
                    {
                        try
                        {
                            var notification = await _notificationService.CreateAccountNotificationAsync(
                                tutorProfile.UserId,
                                NotificationType.TutorApplicationAccepted,
                                $"Đơn ứng tuyển của bạn đã được chấp nhận. Lớp học đã được tạo.",
                                newClass.Id);
                            await _uow.SaveChangesAsync();
                            await _notificationService.SendRealTimeNotificationAsync(tutorProfile.UserId, notification);
                        }
                        catch (Exception notifEx)
                        {
                            // Log but no throw to not affect business logic
                            System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                        }
                    }

                    // send notification to student that class is created
                    var studentProfile = await _mainUow.StudentProfiles.GetByIdAsync(request.StudentId);
                    if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId))
                    {
                        try
                        {
                            var classCreatedNotification = await _notificationService.CreateAccountNotificationAsync(
                                studentProfile.UserId,
                                NotificationType.ClassCreatedFromRequest,
                                "Lớp học đã được tạo từ yêu cầu của bạn. Vui lòng thanh toán để bắt đầu học.",
                                newClass.Id);
                            await _uow.SaveChangesAsync();
                            await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, classCreatedNotification);
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
                    throw; // ApiHandler catches
                }
            });

            return true;
        }

        public async Task<bool> RejectApplicationAsync(string actorUserId, string role, string applicationId)
        {
            var app = await _uow.TutorApplications.GetAsync(
                filter: a => a.Id == applicationId,
                includes: q => q.Include(a => a.ClassRequest) // Include request to check ownership
            );
            if (app == null) throw new KeyNotFoundException("Không tìm thấy đơn ứng tuyển.");

            // Capture validatedRequest result
            var validatedRequest = await ValidateRequestOwnershipAsync(actorUserId, role, app.ClassRequestId);

            if (app.Status != ApplicationStatus.Pending)
                throw new InvalidOperationException("Không thể từ chối đơn này (đã xử lý rồi).");

            // Check against validatedRequest.StudentId
            if (app.ClassRequest == null || app.ClassRequest.StudentId != validatedRequest.StudentId)
                throw new UnauthorizedAccessException("Bạn không có quyền từ chối đơn này.");

            app.Status = ApplicationStatus.Rejected;

            await _uow.TutorApplications.UpdateAsync(app); // Unsave
            await _uow.SaveChangesAsync(); // Save

            // Gửi notification cho tutor khi application bị reject
            var tutorProfile = await _mainUow.TutorProfiles.GetByIdAsync(app.TutorId);
            if (tutorProfile != null && !string.IsNullOrEmpty(tutorProfile.UserId))
            {
                try
                {
                    var notification = await _notificationService.CreateAccountNotificationAsync(
                        tutorProfile.UserId,
                        NotificationType.TutorApplicationRejected,
                        $"Đơn ứng tuyển của bạn đã bị từ chối.",
                        app.Id);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(tutorProfile.UserId, notification);
                }
                catch (Exception notifEx)
                {
                    // Log nhưng không throw để không ảnh hưởng đến business logic
                    System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                }
            }

            return true;
        }

        public async Task<IEnumerable<TutorApplicationResponseDto>> GetApplicationsForMyRequestAsync(string actorUserId, string role, string classRequestId)
        {
            await ValidateRequestOwnershipAsync(actorUserId, role, classRequestId);

            //var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(actorUserId);
            //if (studentProfileId == null)
            //    throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            //// Check if this request is from a student
            //var request = await _uow.ClassRequests.GetByIdAsync(classRequestId);
            //if (request == null || request.StudentId != studentProfileId)
            //    throw new UnauthorizedAccessException("Không tìm thấy yêu cầu hoặc đây không phải yêu cầu của bạn.");

            // Get the applications
            var applications = await _uow.TutorApplications.GetAllAsync(
                filter: a => a.ClassRequestId == classRequestId,
                includes: q => q.Include(a => a.Tutor).ThenInclude(t => t.User) // Include to get name, avatar
            );

            return applications
                .OrderByDescending(a => a.CreatedAt)
                .Select(MapToResponseDto);
        }

        #endregion

        #region Helper

        private TutorApplicationResponseDto MapToResponseDto(TutorApplication app)
        {
            return new TutorApplicationResponseDto
            {
                Id = app.Id,
                ClassRequestId = app.ClassRequestId,
                TutorId = app.TutorId,
                Status = app.Status,
                CreatedAt = app.CreatedAt,
                TutorName = app.Tutor?.User?.UserName, // Include to get name, avatar
                TutorAvatarUrl = app.Tutor?.User?.AvatarUrl, // Include to get name, avatar

                // Update for link tranfer
                MeetingLink = app.MeetingLink
            };
        }

        private async Task<ClassRequest> ValidateRequestOwnershipAsync(string actorUserId, string role, string classRequestId)
        {
            var request = await _uow.ClassRequests.GetAsync(
                filter: r => r.Id == classRequestId,
                includes: q => q.Include(r => r.ClassRequestSchedules)
            );

            if (request == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu lớp học.");

            if (role == "Student")
            {
                var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(actorUserId);
                if (request.StudentId != studentProfileId)
                    throw new UnauthorizedAccessException("Bạn không có quyền thao tác với yêu cầu này.");
            }
            else if (role == "Parent")
            {
                // Check link Parent - Student (request.StudentId)
                var isLinked = await _parentRepo.ExistsLinkAsync(actorUserId, request.StudentId!);
                if (!isLinked)
                    throw new UnauthorizedAccessException("Bạn không có quyền thao tác với yêu cầu của học sinh này.");
            }
            else
            {
                throw new UnauthorizedAccessException("Role không hợp lệ.");
            }

            return request;
        }

        /// <summary>
        /// Kiểm tra duplicate class khi tạo từ ClassRequest (trong TutorApplication)
        /// </summary>
        private async Task CheckForDuplicateClassFromRequestAsync(string tutorId, ClassRequest request)
        {
            if (request.Budget == null)
                return; // Không kiểm tra nếu không có budget

            // Tìm các lớp học của cùng gia sư với cùng môn học, cấp độ và mode
            var existingClasses = await _uow.Classes.GetAllAsync(
                filter: c => c.TutorId == tutorId
                           && c.Subject == request.Subject
                           && c.EducationLevel == request.EducationLevel
                           && c.Mode == request.Mode
                           && c.DeletedAt == null
                           && (c.Status == ClassStatus.Pending || c.Status == ClassStatus.Active || c.Status == ClassStatus.Ongoing),
                includes: q => q.Include(c => c.ClassSchedules)
            );

            if (!existingClasses.Any())
                return; // Không có lớp nào tương tự

            // Kiểm tra giá tương tự (trong khoảng ±10%)
            decimal priceTolerance = request.Budget.Value * 0.1m;
            var similarPriceClasses = existingClasses
                .Where(c => c.Price.HasValue && Math.Abs(c.Price.Value - request.Budget.Value) <= priceTolerance)
                .ToList();

            if (!similarPriceClasses.Any())
                return; // Không có lớp nào có giá tương tự

            // Kiểm tra lịch học trùng lặp
            if (request.ClassRequestSchedules == null || !request.ClassRequestSchedules.Any())
                return;

            foreach (var existingClass in similarPriceClasses)
            {
                if (existingClass.ClassSchedules == null || !existingClass.ClassSchedules.Any())
                    continue;

                // So sánh từng lịch học trong request với các lịch học trong lớp hiện có
                foreach (var newSchedule in request.ClassRequestSchedules)
                {
                    foreach (var existingSchedule in existingClass.ClassSchedules)
                    {
                        // Kiểm tra cùng ngày trong tuần
                        if (existingSchedule.DayOfWeek == newSchedule.DayOfWeek)
                        {
                            // Kiểm tra thời gian chồng chéo
                            if (newSchedule.StartTime < existingSchedule.EndTime && 
                                existingSchedule.StartTime < newSchedule.EndTime)
                            {
                                throw new InvalidOperationException(
                                    $"Đã tồn tại lớp học tương tự (ID: {existingClass.Id}, Tiêu đề: {existingClass.Title}) " +
                                    $"với cùng môn học, cấp độ, mode và lịch học trùng lặp vào {(DayOfWeek)newSchedule.DayOfWeek} " +
                                    $"từ {newSchedule.StartTime:hh\\:mm} đến {newSchedule.EndTime:hh\\:mm}. " +
                                    "Vui lòng kiểm tra lại hoặc hủy lớp học trùng lặp trước khi tạo lớp mới.");
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
