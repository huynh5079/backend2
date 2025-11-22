using BusinessLayer.DTOs.Schedule.ClassRequest;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;

namespace BusinessLayer.Service.ScheduleService;

public class ClassRequestService : IClassRequestService
{
    private readonly IScheduleUnitOfWork _uow;
    private readonly IStudentProfileService _studentProfileService;
    private readonly ITutorProfileService _tutorProfileService;
    private readonly TpeduContext _context;

    public ClassRequestService(
        IScheduleUnitOfWork uow,
        IStudentProfileService studentProfileService,
        ITutorProfileService tutorProfileService,
        TpeduContext context)
    {
        _uow = uow;
        _studentProfileService = studentProfileService;
        _tutorProfileService = tutorProfileService;
        _context = context;
    }

    #region Student's Actions
    public async Task<ClassRequestResponseDto?> CreateClassRequestAsync(string studentUserId, CreateClassRequestDto dto)
    {
        var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
        if (studentProfileId == null)
            throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

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
                    StudentId = studentProfileId,
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
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        return await GetClassRequestByIdAsync(newRequest.Id);
    }
    public async Task<ClassRequestResponseDto?> UpdateClassRequestAsync(string studentUserId, string requestId, UpdateClassRequestDto dto)
    {
        try
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var request = await _uow.ClassRequests.GetAsync(
                cr => cr.Id == requestId && cr.StudentId == studentProfileId);

            if (request == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu hoặc bạn không có quyền sửa.");

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
    public async Task<bool> UpdateClassRequestScheduleAsync(string studentUserId, string requestId, List<ClassRequestScheduleDto> scheduleDtos)
    {
        var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
        if (studentProfileId == null)
            throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

        var request = await _uow.ClassRequests.GetAsync(
            cr => cr.Id == requestId && cr.StudentId == studentProfileId);

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
    public async Task<bool> CancelClassRequestAsync(string studentUserId, string requestId)
    {
        try
        {
            // Validate 
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);

            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var request = await _uow.ClassRequests.GetAsync(
                cr => cr.Id == requestId && cr.StudentId == studentProfileId);

            if (request == null)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu hoặc bạn không có quyền hủy.");

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
    public async Task<IEnumerable<ClassRequestResponseDto>> GetMyClassRequestsAsync(string studentUserId)
    {
        var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
        if (studentProfileId == null)
            return new List<ClassRequestResponseDto>(); // rreturn empty

        var requests = await _uow.ClassRequests.GetAllAsync(
            filter: cr => cr.StudentId == studentProfileId,
            includes: q => q.Include(cr => cr.Student).ThenInclude(s => s.User)
                            .Include(cr => cr.Tutor).ThenInclude(t => t.User)
                            .Include(cr => cr.ClassRequestSchedules)
        );

        return requests.Select(MapToResponseDto);
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

        return requests.Select(MapToResponseDto);
    }

    public async Task<bool> RespondToDirectRequestAsync(string tutorUserId, string requestId, bool accept)
    {
        var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
        if (tutorProfileId == null)
            throw new UnauthorizedAccessException("Tài khoản gia sư không hợp lệ.");

        var request = await _uow.ClassRequests.GetAsync(
            cr => cr.Id == requestId && cr.TutorId == tutorProfileId);

        if (request == null)
            throw new KeyNotFoundException("Không tìm thấy yêu cầu hoặc bạn không có quyền.");

        if (request.Status != ClassRequestStatus.Pending)
            throw new InvalidOperationException("Yêu cầu này đã được xử lý.");

        request.Status = accept ? ClassRequestStatus.Active : ClassRequestStatus.Rejected;

        await _uow.ClassRequests.UpdateAsync(request);
        await _uow.SaveChangesAsync();
        return true;
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
            TutorName = classRequest.Tutor?.User?.UserName,
            Subject = classRequest.Subject,
            EducationLevel = classRequest.EducationLevel,
            // Map list (Entity -> DTO)
            Schedules = classRequest.ClassRequestSchedules.Select(s => new ClassRequestScheduleDto
            {
                DayOfWeek = s.DayOfWeek ?? 0, // byte? to byte
                StartTime = s.StartTime,
                EndTime = s.EndTime
            }).ToList()
        };
    }
}