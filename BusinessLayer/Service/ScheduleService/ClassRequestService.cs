using BusinessLayer.DTOs.Schedule.ClassRequest;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace BusinessLayer.Service.ScheduleService;

public class ClassRequestService : IClassRequestService
{
    private readonly IScheduleUnitOfWork _uow;
    private readonly IUnitOfWork _mainUow;
    private readonly IStudentProfileService _studentProfileService;
    private readonly ITutorProfileService _tutorProfileService;
    private readonly TpeduContext _context;
    private readonly IParentProfileRepository _parentRepo;
    private readonly IScheduleGenerationService _scheduleGenerationService;
    private readonly INotificationService _notificationService;

    public ClassRequestService(
        IScheduleUnitOfWork uow,
        IUnitOfWork mainUow,
        IStudentProfileService studentProfileService,
        ITutorProfileService tutorProfileService,
        TpeduContext context,
        IParentProfileRepository parentRepo,
        IScheduleGenerationService scheduleGenerationService,
        INotificationService notificationService)
    {
        _uow = uow;
        _mainUow = mainUow;
        _studentProfileService = studentProfileService;
        _tutorProfileService = tutorProfileService;
        _context = context;
        _parentRepo = parentRepo;
        _scheduleGenerationService = scheduleGenerationService;
        _notificationService = notificationService;
    }

    #region Student's Actions
    public async Task<ClassRequestResponseDto?> CreateClassRequestAsync(string actorUserId, string userRole, CreateClassRequestDto dto)
    {
        var targetStudentProfileId = await ResolveTargetStudentProfileIdAsync(actorUserId, userRole, dto.StudentUserId);

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        var newRequest = new ClassRequest();

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // create ClassRequest
                newRequest = new ClassRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    StudentId = targetStudentProfileId, // Validated with helper below
                    TutorId = dto.TutorId, // null -> "Marketplace", ID -> "Direct"
                    Budget = dto.Budget,
                    Status = ClassRequestStatus.Pending, // Pending
                    Mode = dto.Mode,
                    ExpiryDate = DateTime.Now.AddDays(7), // set 7 days
                    Description = dto.Description,
                    Location = dto.Location,
                    SpecialRequirements = dto.SpecialRequirements,
                    Subject = dto.Subject,
                    EducationLevel = dto.EducationLevel,
                    ClassStartDate = dto.ClassStartDate?.ToUniversalTime(),
                    OnlineStudyLink = dto.OnlineStudyLink
                };
                await _uow.ClassRequests.CreateAsync(newRequest); // no Save

                // create ClassRequestSchedules
                var newSchedules = dto.Schedules.Select(s => new ClassRequestSchedule
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassRequestId = newRequest.Id,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                }).ToList();

                await _context.ClassRequestSchedules.AddRangeAsync(newSchedules); // Unsave

                // 3. Save
                await _uow.SaveChangesAsync();
                await transaction.CommitAsync();

                // Gửi notification cho tutor nếu là direct request (có TutorId)
                if (!string.IsNullOrEmpty(dto.TutorId))
                {
                    var tutorProfile = await _mainUow.TutorProfiles.GetByIdAsync(dto.TutorId);
                    if (tutorProfile != null && !string.IsNullOrEmpty(tutorProfile.UserId))
                    {
                        try
                        {
                            var studentProfile = await _mainUow.StudentProfiles.GetAsync(
                                filter: s => s.Id == targetStudentProfileId,
                                includes: q => q.Include(s => s.User));
                            var studentName = studentProfile?.User?.UserName ?? "một học sinh";
                            var notification = await _notificationService.CreateAccountNotificationAsync(
                                tutorProfile.UserId,
                                NotificationType.ClassRequestReceived,
                                $"{studentName} đã gửi yêu cầu lớp học '{newRequest.Subject}' cho bạn. Vui lòng xem xét và phản hồi.",
                                newRequest.Id);
                            await _uow.SaveChangesAsync();
                            await _notificationService.SendRealTimeNotificationAsync(tutorProfile.UserId, notification);
                        }
                        catch (Exception notifEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        return await GetClassRequestByIdAsync(newRequest.Id);
    }
    public async Task<ClassRequestResponseDto?> UpdateClassRequestAsync(string actorUserId, string userRole, string requestId, UpdateClassRequestDto dto)
    {
        try
        {
            var request = await ValidateAndGetRequestAsync(actorUserId, userRole, requestId);

            // Only fixable with "Pending"
            if (request.Status != ClassRequestStatus.Pending)
                throw new InvalidOperationException($"Không thể sửa yêu cầu ở trạng thái '{request.Status}'.");

            // Update only provided fields
            if (!string.IsNullOrEmpty(dto.Description))
                request.Description = dto.Description ?? request.Description;
            if (dto.Location != null)
                request.Location = dto.Location;
            if (!string.IsNullOrEmpty(dto.SpecialRequirements))
                request.SpecialRequirements = dto.SpecialRequirements ?? request.SpecialRequirements;
            if (dto.Budget.HasValue)
            request.Budget = dto.Budget ?? request.Budget;
            request.OnlineStudyLink = dto.OnlineStudyLink ?? request.OnlineStudyLink;
            request.Mode = dto.Mode ?? request.Mode;
            request.ClassStartDate = dto.ClassStartDate?.ToUniversalTime() ?? request.ClassStartDate;

            await _uow.ClassRequests.UpdateAsync(request); // Unsave
            await _uow.SaveChangesAsync(); // Save

            return await GetClassRequestByIdAsync(requestId);
        }
        catch (Exception)
        {
            return null;
        }
    }
    public async Task<bool> UpdateClassRequestScheduleAsync(string actorUserId, string userRole, string requestId, List<ClassRequestScheduleDto> scheduleDtos)
    {
        var request = await ValidateAndGetRequestAsync(actorUserId, userRole, requestId);

        if (request == null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu hoặc bạn không có quyền sửa.");

        if (request.Status != ClassRequestStatus.Pending)
            throw new InvalidOperationException($"Không thể sửa lịch của yêu cầu ở trạng thái '{request.Status}'.");

        // transaction 
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // delete old schedules
                var oldSchedules = await _context.ClassRequestSchedules
                    .Where(crs => crs.ClassRequestId == requestId)
                    .ToListAsync();

                _context.ClassRequestSchedules.RemoveRange(oldSchedules);

                // add new schedules
                var newSchedules = scheduleDtos.Select(s => new ClassRequestSchedule
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassRequestId = requestId,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                }).ToList();

                await _context.ClassRequestSchedules.AddRangeAsync(newSchedules);

                // 3. Save
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
    public async Task<bool> CancelClassRequestAsync(string actorUserId, string userRole, string requestId)
    {
        try
        {
            // Validate 
            var request = await ValidateAndGetRequestAsync(actorUserId, userRole, requestId);

            // Only cancel with "Pending"
            if (request.Status != ClassRequestStatus.Pending)
                throw new InvalidOperationException($"Không thể hủy yêu cầu ở trạng thái '{request.Status}'.");

            request.Status = ClassRequestStatus.Cancelled;

            await _uow.ClassRequests.UpdateAsync(request); // Unsave
            await _uow.SaveChangesAsync(); // Save

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public async Task<IEnumerable<ClassRequestResponseDto>> GetMyClassRequestsAsync(string actorUserId, string userRole, string? specificChildId = null)
    {
        List<string> studentProfileIds = new();

        if (userRole == "Student")
        {
            var sid = await _studentProfileService.GetStudentProfileIdByUserIdAsync(actorUserId);
            if (sid != null) studentProfileIds.Add(sid);
        }
        else if (userRole == "Parent")
        {
            // Parent: take all children or specific child
            if (!string.IsNullOrEmpty(specificChildId))
            {
                // if specific child, validate link
                var isLinked = await _parentRepo.ExistsLinkAsync(actorUserId, specificChildId);
                if (!isLinked)
                    throw new UnauthorizedAccessException("Bạn không có quyền xem yêu cầu của học sinh này.");

                studentProfileIds.Add(specificChildId);
            }
            else
            {
                var childrenIds = await _parentRepo.GetChildrenIdsAsync(actorUserId);
                studentProfileIds.AddRange(childrenIds);
            }
        }

        if (!studentProfileIds.Any())
            return new List<ClassRequestResponseDto>(); // return empty

        var requests = await _uow.ClassRequests.GetAllAsync(
            filter: cr => studentProfileIds.Contains(cr.StudentId!) && cr.DeletedAt == null,
            includes: q => q.Include(cr => cr.Student).ThenInclude(s => s.User)
                            .Include(cr => cr.Tutor).ThenInclude(t => t.User)
                            .Include(cr => cr.ClassRequestSchedules)
        );

        return requests
            .OrderByDescending(cr => cr.CreatedAt)
            .Select(MapToResponseDto);
    }

    #endregion

    #region Tutor's Actions
    public async Task<IEnumerable<ClassRequestResponseDto>> GetDirectRequestsAsync(string tutorUserId)
    {
        var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
        if (tutorProfileId == null)
            return new List<ClassRequestResponseDto>();

        var requests = await _uow.ClassRequests.GetAllAsync(
            filter: cr => cr.TutorId == tutorProfileId && cr.Status == ClassRequestStatus.Pending,
            includes: q => q.Include(cr => cr.Student).ThenInclude(s => s.User)
                            .Include(cr => cr.Tutor).ThenInclude(t => t.User)
                            .Include(cr => cr.ClassRequestSchedules)
        );

        return requests
            .OrderByDescending(cr => cr.CreatedAt)
            .Select(MapToResponseDto);
    }

    public async Task<string?> RespondToDirectRequestAsync(string tutorUserId, string requestId, bool accept, string? meetingLink = null)
    {
        var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
        if (tutorProfileId == null)
            throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

        var request = await _uow.ClassRequests.GetAsync(
            cr => cr.Id == requestId && cr.TutorId == tutorProfileId,
            includes: q => q.Include(cr => cr.ClassRequestSchedules));

        if (request == null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu hoặc bạn không có quyền.");

        if (request.Status != ClassRequestStatus.Pending)
            throw new InvalidOperationException("Yêu cầu này đã được xử lý.");

        if (!accept)
        {
            // Reject: only update status
            request.Status = ClassRequestStatus.Rejected;
            await _uow.ClassRequests.UpdateAsync(request);
            await _uow.SaveChangesAsync();

            // Gửi notification cho student khi request bị reject
            var studentProfile = await _mainUow.StudentProfiles.GetByIdAsync(request.StudentId ?? string.Empty);
            if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId))
            {
                try
                {
                    var notification = await _notificationService.CreateAccountNotificationAsync(
                        studentProfile.UserId,
                        NotificationType.ClassRequestRejected,
                        "Yêu cầu lớp học của bạn đã bị gia sư từ chối.",
                        request.Id);
                    await _uow.SaveChangesAsync();
                    await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, notification);
                }
                catch (Exception notifEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send notification: {notifEx.Message}");
                }
            }

            return null; // Reject không tạo Class, trả về null
        }

        // Accept: create Class + ClassAssign + Schedule
        string? createdClassId = null;
        var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Kiểm tra duplicate class trước khi tạo
                    await CheckForDuplicateClassFromRequestAsync(tutorProfileId, request);

                    // 1. Tạo Class từ ClassRequest
                    var newClass = new Class
                {
                    Id = Guid.NewGuid().ToString(),
                    TutorId = tutorProfileId,
                    Title = $"Lớp {request.Subject} (từ yêu cầu {request.Id})",
                    Description = $"{request.Description}\n\nYêu cầu đặc biệt: {request.SpecialRequirements}",
                    Price = request.Budget,
                    Status = ClassStatus.Pending, // Chờ học sinh thanh toán
                    Location = request.Location,
                    Mode = request.Mode,
                    Subject = request.Subject,
                    EducationLevel = request.EducationLevel,
                    ClassStartDate = request.ClassStartDate,
                    StudentLimit = 1, // 1-1
                    CurrentStudentCount = 1,
                    // link tranfer
                    OnlineStudyLink = !string.IsNullOrEmpty(meetingLink) ? meetingLink : request.OnlineStudyLink
                };
                await _uow.Classes.CreateAsync(newClass);
                // save ClassId for closure
                createdClassId = newClass.Id;

                // create ClassAssign vs PaymentStatus = Pending
                var newAssignment = new ClassAssign
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassId = newClass.Id,
                    StudentId = request.StudentId,
                    PaymentStatus = PaymentStatus.Pending, // unpaid, will navigate to payment
                    ApprovalStatus = ApprovalStatus.Approved, // Tutor accepted, request = Approved
                    EnrolledAt = DateTime.UtcNow
                };
                await _uow.ClassAssigns.CreateAsync(newAssignment);

                // copy schedule from ClassRequestSchedules to ClassSchedules
                var newClassSchedules = request.ClassRequestSchedules.Select(reqSchedule => new ClassSchedule
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassId = newClass.Id,
                    DayOfWeek = reqSchedule.DayOfWeek ?? 0,
                    StartTime = reqSchedule.StartTime,
                    EndTime = reqSchedule.EndTime
                }).ToList();
                await _context.ClassSchedules.AddRangeAsync(newClassSchedules);

                // update request status
                request.Status = ClassRequestStatus.Matched; // matched tutor and created class
                await _uow.ClassRequests.UpdateAsync(request);

                // call schedule generation
                await _scheduleGenerationService.GenerateScheduleFromRequestAsync(
                    newClass.Id,
                    tutorProfileId,
                    request.ClassStartDate ?? DateTime.UtcNow,
                    request.ClassRequestSchedules
                );

                // save all
                await _uow.SaveChangesAsync();
                await transaction.CommitAsync();

                // Gửi notification cho student khi request được accept và class được tạo
                var studentProfile = await _mainUow.StudentProfiles.GetByIdAsync(request.StudentId ?? string.Empty);
                if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId) && createdClassId != null)
                {
                    try
                    {
                        var acceptNotification = await _notificationService.CreateAccountNotificationAsync(
                            studentProfile.UserId,
                            NotificationType.ClassRequestAccepted,
                            "Yêu cầu lớp học của bạn đã được gia sư chấp nhận.",
                            request.Id);
                        await _uow.SaveChangesAsync();
                        await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, acceptNotification);

                        var createdNotification = await _notificationService.CreateAccountNotificationAsync(
                            studentProfile.UserId,
                            NotificationType.ClassCreatedFromRequest,
                            "Lớp học đã được tạo từ yêu cầu của bạn. Vui lòng thanh toán để bắt đầu học.",
                            createdClassId);
                        await _uow.SaveChangesAsync();
                        await _notificationService.SendRealTimeNotificationAsync(studentProfile.UserId, createdNotification);
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

        return createdClassId; // return created ClassId
    }

    #endregion

    #region Public/Shared Actions

    public async Task<ClassRequestResponseDto?> GetClassRequestByIdAsync(string id)
    {
        var request = await _uow.ClassRequests.GetAsync(
            filter: cr => cr.Id == id,
            includes: q => q.Include(cr => cr.Student).ThenInclude(s => s.User)
                            .Include(cr => cr.Tutor).ThenInclude(t => t.User)
                            .Include(cr => cr.ClassRequestSchedules) // <-- Load schedules
        );

        if (request == null) return null;

        return MapToResponseDto(request);
    }

    public async Task<(IEnumerable<ClassRequestResponseDto> Data, int TotalCount)> GetMarketplaceRequestsAsync(
        int page, int pageSize, string? status, string? subject,
        string? educationLevel, string? mode, string? locationContains)
    {
        // Parse Enums
        Enum.TryParse<ClassRequestStatus>(status, true, out var statusEnum);
        Enum.TryParse<ClassMode>(mode, true, out var modeEnum);

        // start query
        IQueryable<ClassRequest> query = _context.ClassRequests
            .Where(cr => cr.DeletedAt == null && cr.TutorId == null); // only Marketplace

        // Apply filters
        if (!string.IsNullOrEmpty(status))
            query = query.Where(cr => cr.Status == statusEnum);
        else // default to Pending
            query = query.Where(cr => cr.Status == ClassRequestStatus.Pending);

        if (!string.IsNullOrEmpty(subject))
            query = query.Where(cr => cr.Subject != null && cr.Subject.Contains(subject));

        if (!string.IsNullOrEmpty(educationLevel))
            query = query.Where(cr => cr.EducationLevel != null && cr.EducationLevel.Contains(educationLevel));

        if (!string.IsNullOrEmpty(mode))
            query = query.Where(cr => cr.Mode == modeEnum);

        if (!string.IsNullOrEmpty(locationContains))
            query = query.Where(cr => cr.Location != null && cr.Location.Contains(locationContains));

        // Count
        var totalCount = await query.CountAsync();

        // Paginate
        var pagedData = await query
            .Include(cr => cr.Student).ThenInclude(s => s.User)
            .Include(cr => cr.Tutor).ThenInclude(t => t.User)
            .Include(cr => cr.ClassRequestSchedules)
            .OrderByDescending(cr => cr.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (pagedData.Select(MapToResponseDto), totalCount);
    }

    #endregion

    #region Admin/System Actions

    public async Task<bool> UpdateClassRequestStatusAsync(string id, UpdateStatusDto dto)
    {
        try
        {
            var request = await _uow.ClassRequests.GetByIdAsync(id);
            if (request == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu.");


            // TODO: add business rules here to restrict status changes

            request.Status = dto.Status; // enum from DTO
            await _uow.ClassRequests.UpdateAsync(request);
            await _uow.SaveChangesAsync();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<int> ExpireClassRequestsAsync()
    {
        try
        {
            // find all active requests past expiry date
            var expiredRequests = await _uow.ClassRequests.GetAllAsync(
                filter: cr => cr.Status == ClassRequestStatus.Active &&
                              cr.ExpiryDate != null &&
                              cr.ExpiryDate <= DateTime.Now);

            if (!expiredRequests.Any()) return 0;

            foreach (var request in expiredRequests)
            {
                request.Status = ClassRequestStatus.Expired;
                await _uow.ClassRequests.UpdateAsync(request);
            }

            return await _uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error expiring class requests: {ex.Message}");
            return 0;
        }
    }

    public async Task<bool> DeleteClassRequestAsync(string id)
    {
        try
        {
            var classRequest = await _uow.ClassRequests.GetByIdAsync(id);
            if (classRequest == null || classRequest.DeletedAt != null) return false;

            classRequest.DeletedAt = DateTime.Now;
            classRequest.UpdatedAt = DateTime.Now;
            await _uow.ClassRequests.UpdateAsync(classRequest);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion

    // Helper
    private static ClassRequestResponseDto MapToResponseDto(ClassRequest classRequest)
    {
        return new ClassRequestResponseDto
        {
            Id = classRequest.Id,
            Description = classRequest.Description,
            Location = classRequest.Location,
            SpecialRequirements = classRequest.SpecialRequirements,
            Budget = classRequest.Budget ?? 0,
            OnlineStudyLink = classRequest.OnlineStudyLink,
            Status = classRequest.Status,
            Mode = classRequest.Mode,
            ClassStartDate = classRequest.ClassStartDate,
            ExpiryDate = classRequest.ExpiryDate,
            CreatedAt = classRequest.CreatedAt,
            StudentName = classRequest.Student?.User?.UserName,
            TutorId = classRequest.TutorId,
            TutorUserId = classRequest.Tutor.UserId,
            TutorName = classRequest.Tutor?.User?.UserName,
            Subject = classRequest.Subject,
            EducationLevel = classRequest.EducationLevel,
            // Map list (Entity to DTO)
            Schedules = classRequest.ClassRequestSchedules.Select(s => new ClassRequestScheduleDto
            {
                DayOfWeek = s.DayOfWeek ?? 0, // byte? to byte
                StartTime = s.StartTime,
                EndTime = s.EndTime
            }).ToList()
        };
    }

    // Resolve StudentProfileId from UserID and Role
    private async Task<string> ResolveTargetStudentProfileIdAsync(string actorUserId, string userRole, string? targetStudentUserId)
    {
        // If Student: take their own profile
        if (userRole == "Student")
        {
            var profileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(actorUserId);
            if (string.IsNullOrEmpty(profileId))
                throw new KeyNotFoundException("Không tìm thấy hồ sơ học sinh của bạn.");
            return profileId;
        }

        // if Parent: need to specify StudentUserId
        if (userRole == "Parent")
        {
            if (string.IsNullOrEmpty(targetStudentUserId))
                throw new ArgumentException("Phụ huynh cần chọn học sinh (StudentUserId) để thực hiện thao tác.");

            // take target StudentProfileId based on UserID
            var childProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(targetStudentUserId);
            if (string.IsNullOrEmpty(childProfileId))
                throw new KeyNotFoundException("Không tìm thấy hồ sơ học sinh này.");

            // validate link Parent - Student
            var isLinked = await _parentRepo.ExistsLinkAsync(actorUserId, childProfileId);
            if (!isLinked)
                throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên hồ sơ học sinh này.");

            return childProfileId;
        }

        throw new UnauthorizedAccessException("Role không hợp lệ.");
    }
    private async Task<ClassRequest> ValidateAndGetRequestAsync(string actorUserId, string userRole, string requestId)
    {
        // Request from db
        var request = await _uow.ClassRequests.GetAsync(r => r.Id == requestId);
        if (request == null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu lớp học.");

        // check authorization
        if (userRole == "Student")
        {
            // if Student: check if request.StudentId matches their profile
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(actorUserId);
            if (request.StudentId != studentProfileId)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa yêu cầu này.");

        }
        else if (userRole == "Parent")
        {
            // If Parent: check if request.StudentId is linked to their children StudentProfileId
            var isLinked = await _parentRepo.ExistsLinkAsync(actorUserId, request.StudentId!);
            if (!isLinked)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa yêu cầu của học sinh này.");
        }
        else
        {
            // Other roles not allowed
            throw new UnauthorizedAccessException("Role không hợp lệ.");
        }

        return request;
    }

    /// <summary>
    ///  Check for duplicate class based on ClassRequest details
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

}