using BusinessLayer.DTOs.Schedule.ClassSchedule;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum; // (Cần dùng ClassStatus)
using DataLayer.Repositories.Abstraction; // (Cần dùng IUnitOfWork)
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class ClassScheduleService : IClassScheduleService
    {
        // --- Sửa Dependencies ---
        private readonly IUnitOfWork _uow;
        private readonly TpeduContext _context;
        private readonly ITutorProfileService _tutorProfileService;

        public ClassScheduleService(
            IUnitOfWork uow,
            TpeduContext context,
            ITutorProfileService tutorProfileService)
        {
            _uow = uow;
            _context = context;
            _tutorProfileService = tutorProfileService;
        }

        /// <summary>
        /// (Gia sư) Tạo một Lớp học (Template) mới
        /// *** PHIÊN BẢN ĐÃ SỬA THEO KIẾN TRÚC MỚI ***
        /// </summary>
        public async Task<ClassDto> CreateRecurringClassScheduleAsync(string tutorUserId, CreateClassScheduleDto createDto)
        {
            // 1. Lấy TutorProfileId (vì service này chỉ dùng cho Tutor)
            var tutorProfileId = await _tutorProfileService.GetTutorProfileIdByUserIdAsync(tutorUserId);
            if (tutorProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản chưa có hồ sơ gia sư.");

            // 2. Validation
            foreach (var rule in createDto.ScheduleRules)
            {
                if (rule.EndTime <= rule.StartTime)
                {
                    throw new ArgumentException($"Lịch học {rule.DayOfWeek} có giờ kết thúc sớm hơn giờ bắt đầu.");
                }
            }

            // 3. Tạo Entity Class (dùng DTO mới)
            var newClass = new Class
            {
                Id = Guid.NewGuid().ToString(),
                TutorId = tutorProfileId,
                Title = createDto.Title,
                Description = createDto.Description,
                Status = ClassStatus.Pending.ToString(), // Luôn là Pending
                Price = createDto.Price,

                // Map các trường mới
                Subject = createDto.Subject,
                EducationLevel = createDto.EducationLevel,
                Mode = createDto.Mode,
                Location = createDto.Location,
                StudentLimit = createDto.StudentLimit,
                CurrentStudentCount = 0, // Mới tạo
                ClassStartDate = createDto.ClassStartDate?.ToUniversalTime(),
                OnlineStudyLink = createDto.OnlineStudyLink,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 4. Tạo Entity ClassSchedule
            var newScheduleRules = createDto.ScheduleRules.Select(ruleDto => new ClassSchedule
            {
                Id = Guid.NewGuid().ToString(),
                ClassId = newClass.Id,
                DayOfWeek = (byte)ruleDto.DayOfWeek,
                StartTime = ruleDto.StartTime, // TimeOnly -> TimeOnly
                EndTime = ruleDto.EndTime,     // TimeOnly -> TimeOnly
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();


            // 5. Dùng Transaction
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Dùng repo (KHÔNG TỰ SAVE)
                    await _uow.Classes.CreateAsync(newClass);
                    // Dùng context để AddRange (vì ta chưa tạo repo cho ClassSchedule)
                    await _context.ClassSchedules.AddRangeAsync(newScheduleRules);

                    // Save 1 lần
                    await _uow.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            // 6. Map trả về
            return MapToClassDto(newClass, createDto.ScheduleRules);
        }

        // --- Helper Map (Dùng DTO mới) ---
        private ClassDto MapToClassDto(Class newClass, List<RecurringScheduleRuleDto> rules)
        {
            return new ClassDto
            {
                Id = newClass.Id,
                TutorId = newClass.TutorId,
                Title = newClass.Title ?? "N/A",
                Description = newClass.Description,
                Price = newClass.Price,
                Status = newClass.Status ?? "N/A",
                CreatedAt = newClass.CreatedAt,
                UpdatedAt = newClass.UpdatedAt,

                // Map các trường mới
                Subject = newClass.Subject,
                EducationLevel = newClass.EducationLevel,
                Location = newClass.Location,
                CurrentStudentCount = newClass.CurrentStudentCount,
                StudentLimit = newClass.StudentLimit,
                Mode = newClass.Mode,
                ClassStartDate = newClass.ClassStartDate,
                OnlineStudyLink = newClass.OnlineStudyLink,

                ScheduleRules = rules // Trả về các quy tắc vừa tạo
            };
        }
    }
}