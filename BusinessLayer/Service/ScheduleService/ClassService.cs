using BusinessLayer.DTOs.Schedule.Class;
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
    public class ClassService : IClassService
    {
        private readonly IScheduleUnitOfWork _uow;
        private readonly TpeduContext _context;
        private readonly ITutorProfileService _tutorProfileService;

        public ClassService(
            IScheduleUnitOfWork uow,
            TpeduContext context,
            ITutorProfileService tutorProfileService)
        {
            _uow = uow;
            _context = context;
            _tutorProfileService = tutorProfileService;
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
            return classes.Select(cls => MapToClassDto(cls));
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
            return classes.Select(cls => MapToClassDto(cls));
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

        #endregion

        #region Helper

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