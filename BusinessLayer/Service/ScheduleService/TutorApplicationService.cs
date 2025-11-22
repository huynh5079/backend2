using BusinessLayer.DTOs.Schedule.TutorApplication;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
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
        private readonly TpeduContext _context;
        private readonly ITutorProfileService _tutorProfileService;
        private readonly IStudentProfileService _studentProfileService;
        private readonly IScheduleGenerationService _scheduleGenerationService;

        public TutorApplicationService(
            IScheduleUnitOfWork uow,
            TpeduContext context,
            ITutorProfileService tutorProfileService,
            IStudentProfileService studentProfileService,
            IScheduleGenerationService scheduleGenerationService)
        {
            _uow = uow;
            _context = context;
            _tutorProfileService = tutorProfileService;
            _studentProfileService = studentProfileService;
            _scheduleGenerationService = scheduleGenerationService;
        }

        #region Tutor's Actions

        public async Task<TutorApplicationResponseDto?> CreateApplicationAsync(string tutorUserId, CreateTutorApplicationDto dto)
        {
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

            // Check if ClassRequest exists and is Active   
            var classRequest = await _uow.ClassRequests.GetByIdAsync(dto.ClassRequestId);
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
                Status = ApplicationStatus.Pending
            };

            await _uow.TutorApplications.CreateAsync(newApplication); // Unsave
            await _uow.SaveChangesAsync(); // Save

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

            return applications.Select(MapToResponseDto);
        }

        #endregion

        #region Student's Actions

        /// <summary>
        /// [CORE TRANSACTION] Student accepts 1 application from Tutor
        /// </summary>
        public async Task<bool> AcceptApplicationAsync(string studentUserId, string applicationId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

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

                    // 2. Validation
                    if (application == null)
                        throw new KeyNotFoundException("Đơn ứng tuyển không tồn tại.");
                    if (application.ClassRequest == null)
                        throw new KeyNotFoundException("Yêu cầu lớp học liên quan không tồn tại.");
                    if (application.ClassRequest.StudentId != studentProfileId)
                        throw new UnauthorizedAccessException("Bạn không có quyền chấp nhận đơn này.");
                    if (application.Status != ApplicationStatus.Pending)
                        throw new InvalidOperationException("Đơn này đã được xử lý.");

                    var request = application.ClassRequest;

                    // CALL PAYMENT LOGIC
                    // (Temporarily ignore as agreed)
                    // var paymentResult = await _paymentService.ProcessClassPayment(studentProfileId, request.Budget ?? 0);
                    // if (!paymentResult.IsSuccess)
                    // throw new InvalidOperationException(paymentResult.Message);

                    // --- START CREATE NEW CLASS ---

                    // CREATE CLASS
                    var newClass = new Class
                    {
                        Id = Guid.NewGuid().ToString(),
                        TutorId = application.TutorId, // Get TutorId from application
                        Title = $"Lớp {request.Subject} (từ yêu cầu {request.Id})",
                        Description = $"{request.Description}\n\nYêu cầu đặc biệt: {request.SpecialRequirements}",
                        Price = request.Budget,
                        Status = ClassStatus.Ongoing, // Class starts now
                        Location = request.Location,
                        Mode = request.Mode,
                        Subject = request.Subject,
                        EducationLevel = request.EducationLevel,
                        ClassStartDate = request.ClassStartDate,
                        OnlineStudyLink = request.OnlineStudyLink,
                        StudentLimit = 1, // 1-1
                        CurrentStudentCount = 1 
                    };
                    await _uow.Classes.CreateAsync(newClass); // no save

                    // CLASSASSIGN
                    var newAssignment = new ClassAssign
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClassId = newClass.Id,
                        StudentId = studentProfileId,
                        PaymentStatus = PaymentStatus.Paid, // payment was made in step 3
                        ApprovalStatus = ApprovalStatus.Approved,
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
                    request.Status = ClassRequestStatus.Ongoing; // Paid and studying

                    await _uow.TutorApplications.UpdateAsync(application); // Unsave
                    await _uow.ClassRequests.UpdateAsync(request); // Unsave

                    // CREATE CALENDAR (LESSONS AND SCHEDULE ENTRIES)
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
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw; // ApiHandler catches
                }
            });

            return true;
        }

        public async Task<bool> RejectApplicationAsync(string studentUserId, string applicationId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var application = await _uow.TutorApplications.GetAsync(
                filter: a => a.Id == applicationId,
                includes: q => q.Include(app => app.ClassRequest)
            );

            if (application == null)
                throw new KeyNotFoundException("Đơn ứng tuyển không tồn tại.");
            if (application.ClassRequest == null || application.ClassRequest.StudentId != studentProfileId)
                throw new UnauthorizedAccessException("Bạn không có quyền từ chối đơn này.");
            if (application.Status != ApplicationStatus.Pending)
                throw new InvalidOperationException("Đơn này đã được xử lý.");

            application.Status = ApplicationStatus.Rejected;

            await _uow.TutorApplications.UpdateAsync(application); // Unsave
            await _uow.SaveChangesAsync(); // Save
            return true;
        }

        public async Task<IEnumerable<TutorApplicationResponseDto>> GetApplicationsForMyRequestAsync(string studentUserId, string classRequestId)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            // Check if this request is from a student
            var request = await _uow.ClassRequests.GetByIdAsync(classRequestId);
            if (request == null || request.StudentId != studentProfileId)
                throw new UnauthorizedAccessException("Không tìm thấy yêu cầu hoặc đây không phải yêu cầu của bạn.");

            // Get the applications
            var applications = await _uow.TutorApplications.GetAllAsync(
                filter: a => a.ClassRequestId == classRequestId,
                includes: q => q.Include(a => a.Tutor).ThenInclude(t => t.User) // Include to get name, avatar
            );

            return applications.Select(MapToResponseDto);
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
                TutorAvatarUrl = app.Tutor?.User?.AvatarUrl // Include to get name, avatar
            };
        }

        #endregion
    }
}
