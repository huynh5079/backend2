using BusinessLayer.DTOs.Schedule.ScheduleEntry;
using BusinessLayer.Service.Interface;
using BusinessLayer.DTOs.Profile;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace BusinessLayer.Service.ScheduleService
{
    public class ScheduleViewService : IScheduleViewService
    {
        private readonly IScheduleUnitOfWork _uow;
        private readonly IStudentProfileService _studentProfileService;
        private readonly IParentChildrenService _parentChildrenService;

        public ScheduleViewService(
            IScheduleUnitOfWork uow,
            IStudentProfileService studentProfileService,
            IParentChildrenService parentChildrenService)
        {
            _uow = uow;
            _studentProfileService = studentProfileService; 
            _parentChildrenService = parentChildrenService;
        }

        public async Task<IEnumerable<ScheduleEntryDto>> GetTutorScheduleAsync(
            string tutorId, 
            DateTime startDate, 
            DateTime endDate, 
            string? entryType,
            string? classId = null,
            string? filterByStudentId = null, 
            string? classStatus = null)
        {
            //  startDate, endDate to UTC, endDate is full day
            var startUtc = startDate.Date.ToUniversalTime();
            // endDate ưill be 00:00 the next day in UTC
            var endUtc = endDate.Date.AddDays(1).ToUniversalTime();

            // Parse EntryType
            EntryType? targetEntryType = null;
            if (!string.IsNullOrEmpty(entryType) && Enum.TryParse<EntryType>(entryType.ToUpper(), out var type))
            {
                targetEntryType = type;
            }
            // Parse ClassStatus
            ClassStatus? targetClassStatus = null;
            if (!string.IsNullOrEmpty(classStatus) && Enum.TryParse<ClassStatus>(classStatus, true, out var status))
            {
                targetClassStatus = status;
            }
            // Apply classId filter if provided
            // Only applies to LESSON entries, BLOCK entries don't have Lesson/Class
            Expression<Func<ScheduleEntry, bool>> filter = se =>
                se.TutorId == tutorId &&
                se.DeletedAt == null &&
                se.StartTime < endUtc &&
                se.EndTime > startUtc &&
                // Filter Class
                (string.IsNullOrEmpty(classId) || (se.Lesson != null && se.Lesson.ClassId == classId)) &&
                // Filter EntryType
                (targetEntryType == null || se.EntryType == targetEntryType) &&
                // Filter Student
                (string.IsNullOrEmpty(filterByStudentId) || (se.Lesson != null && se.Lesson.Class.ClassAssigns.Any(ca => ca.StudentId == filterByStudentId))) &&
                (targetClassStatus == null || (se.Lesson != null && se.Lesson.Class.Status == targetClassStatus));

            // define includes
            Func<IQueryable<ScheduleEntry>, IQueryable<ScheduleEntry>> includes = query =>
                query.Include(se => se.Lesson)
                        .ThenInclude(l => l.Class)
                            .ThenInclude(c => c.ClassAssigns) // Include to check if belong to this class
                     .Include(se => se.Block)
                     .OrderBy(se => se.StartTime);

            // Query
            var scheduleEntries = await _uow.ScheduleEntries.GetAllAsync(
                filter: filter,
                includes: includes
            );

            // Map DTO
            return scheduleEntries.Select(se => new ScheduleEntryDto
            {
                Id = se.Id,
                TutorId = se.TutorId,
                StartTime = se.StartTime, // Return to UTC
                EndTime = se.EndTime,     // Return to UTC
                EntryType = se.EntryType,
                LessonId = se.LessonId,
                ClassId = se.Lesson?.ClassId,
                Title = se.EntryType == EntryType.LESSON ? se.Lesson?.Title : "Lịch rảnh",
                AttendanceStatus = null
            });
        }

        public async Task<IEnumerable<ScheduleEntryDto>> GetStudentScheduleAsync(
            string studentUserId, 
            DateTime startDate, 
            DateTime endDate,
            string? filterByTutorId = null, 
            string? classStatus = null)
        {
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (string.IsNullOrEmpty(studentProfileId))
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            // Reuse logic nội bộ để tránh lặp code và đảm bảo ClassStatus được xử lý
            return await GetScheduleByStudentProfileIdInternal(studentProfileId, startDate, endDate, filterByTutorId, classStatus);
        }

        public async Task<IEnumerable<ScheduleEntryDto>> GetChildScheduleAsync(
            string parentUserId,
            string childProfileId,
            DateTime startDate,
            DateTime endDate,
            string? filterByTutorId = null,
            string? classStatus = null)
        {
            // validate parent-child relationship
            var isChild = await _parentChildrenService.IsChildOfParentAsync(parentUserId, childProfileId);
            if (!isChild)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xem lịch của học sinh này.");
            }

            // 2. Lấy UserId của đứa trẻ (Vì hàm GetStudentScheduleAsync đang nhận UserId)
            // Cần thêm hàm GetUserIdByProfileId trong StudentProfileService nếu chưa có.
            // HOẶC: Sửa GetStudentScheduleAsync để nhận ProfileId trực tiếp (tốt hơn).

            // Cách nhanh nhất hiện tại: Reuse logic query của GetStudentScheduleAsync nhưng viết lại đoạn đầu
            // Để tránh phụ thuộc UserId, mình sẽ tách logic query ra private method (Refactoring).

            return await GetScheduleByStudentProfileIdInternal(childProfileId, startDate, endDate, filterByTutorId, classStatus);
        }

        public async Task<IEnumerable<ScheduleEntryDto>> GetAllChildrenScheduleAsync(
            string parentUserId,
            DateTime startDate,
            DateTime endDate,
            string? filterByTutorId = null,
            string? classStatus = null)
        {
            // take all children IDs of this parent
            var childrenIds = await _parentChildrenService.GetChildrenInfoByParentUserIdAsync(parentUserId);

            if (!childrenIds.Any()) return new List<ScheduleEntryDto>();

            var allSchedules = new List<ScheduleEntryDto>();

            // loop through each child and get their schedule
            // can be optimized with when parallel tasks if needed
            foreach (var child in childrenIds)
            {
                var childSchedule = await GetScheduleByStudentProfileIdInternal(child.StudentId, startDate, endDate, filterByTutorId, classStatus);

                // create new list to avoid modifying reference
                var labeledSchedule = childSchedule.Select(s =>
                {
                    // Clone
                    return new ScheduleEntryDto
                    {
                        Id = s.Id,
                        TutorId = s.TutorId,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        EntryType = s.EntryType,
                        LessonId = s.LessonId,
                        ClassId = s.ClassId,
                        AttendanceStatus = s.AttendanceStatus,
                        Title = $"[{child.FullName}] {s.Title}"
                    };
                });

                allSchedules.AddRange(childSchedule);
            }

            // Sort all entries by StartTime before returning
            return allSchedules.OrderBy(s => s.StartTime);
        }

        // query based on user directly
        private async Task<IEnumerable<ScheduleEntryDto>> GetScheduleByStudentProfileIdInternal(
            string studentProfileId,
            DateTime startDate,
            DateTime endDate,
            string? filterByTutorId = null, 
            string? classStatus = null)
        {
            var startUtc = startDate.Date.ToUniversalTime();
            var endUtc = endDate.Date.AddDays(1).ToUniversalTime();

            // 1. Parse ClassStatus (CẦN BỔ SUNG ĐOẠN NÀY)
            ClassStatus? targetClassStatus = null;
            if (!string.IsNullOrEmpty(classStatus) && Enum.TryParse<ClassStatus>(classStatus, true, out var status))
            {
                targetClassStatus = status;
            }

            var scheduleEntries = await _uow.ScheduleEntries.GetAllAsync(
                filter: se =>
                    se.DeletedAt == null &&
                    se.EntryType == EntryType.LESSON &&
                    se.StartTime < endUtc &&
                    se.EndTime > startUtc &&
                    se.Lesson.Class.ClassAssigns.Any(ca => ca.StudentId == studentProfileId) &&
                    // filter by TutorId if provided
                    (string.IsNullOrEmpty(filterByTutorId) || se.TutorId == filterByTutorId) &&
                    // Check Class Status
                    (targetClassStatus == null || se.Lesson.Class.Status == targetClassStatus),

                includes: query => query
                    .Include(se => se.Lesson)
                        .ThenInclude(l => l.Class)
                            .ThenInclude(c => c.ClassAssigns)
                    .Include(se => se.Lesson)
                        .ThenInclude(l => l.Attendances)
                    .OrderBy(se => se.StartTime)
            );

            return scheduleEntries.Select(se =>
            {
                var myAttendance = se.Lesson?.Attendances?
                    .FirstOrDefault(a => a.StudentId == studentProfileId);
                return new ScheduleEntryDto
                {
                    Id = se.Id,
                    TutorId = se.TutorId,
                    StartTime = se.StartTime,
                    EndTime = se.EndTime,
                    EntryType = se.EntryType,
                    LessonId = se.LessonId,
                    ClassId = se.Lesson?.ClassId,
                    Title = se.Lesson?.Title,
                    AttendanceStatus = myAttendance?.Status.ToString(),
                };
            });
        }
    }
}
