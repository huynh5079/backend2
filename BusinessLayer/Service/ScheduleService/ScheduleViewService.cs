using BusinessLayer.DTOs.Schedule.ScheduleEntry;
using BusinessLayer.Service.Interface;
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

        public ScheduleViewService(
            IScheduleUnitOfWork uow,
            IStudentProfileService studentProfileService)
        {
            _uow = uow;
            _studentProfileService = studentProfileService;
        }

        public async Task<IEnumerable<ScheduleEntryDto>> GetTutorScheduleAsync(
            string tutorId, 
            DateTime startDate, 
            DateTime endDate, 
            string? entryType)
        {
            //  startDate, endDate to UTC, endDate is full day
            var startUtc = startDate.Date.ToUniversalTime();
            // endDate ưill be 00:00 the next day in UTC
            var endUtc = endDate.Date.AddDays(1).ToUniversalTime();

            // define includes with Block
            Func<IQueryable<ScheduleEntry>, IQueryable<ScheduleEntry>> includes = query =>
                query.Include(se => se.Lesson)
                     .Include(se => se.Block) // <-- Thêm Include này
                     .OrderBy(se => se.StartTime);

            // create base filter expression
            Expression<Func<ScheduleEntry, bool>> baseFilter = se =>
                se.TutorId == tutorId &&
                se.DeletedAt == null &&
                se.StartTime < endUtc &&
                se.EndTime > startUtc;

            IEnumerable<ScheduleEntry> scheduleEntries;

            // decide which query to run based on entryType
            if (!string.IsNullOrEmpty(entryType))
            {
                if (Enum.TryParse<EntryType>(entryType.ToUpper(), out var type))
                {
                    // Do 2 separate queries for LESSON and BLOCK
                    if (type == EntryType.LESSON)
                    {
                        scheduleEntries = await _uow.ScheduleEntries.GetAllAsync(
                            filter: se => se.TutorId == tutorId &&
                                         se.DeletedAt == null &&
                                         se.StartTime < endUtc &&
                                         se.EndTime > startUtc &&
                                         se.EntryType == EntryType.LESSON, // filter LESSON
                            includes: includes
                        );
                    }
                    else // (type == EntryType.BLOCK)
                    {
                        scheduleEntries = await _uow.ScheduleEntries.GetAllAsync(
                            filter: se => se.TutorId == tutorId &&
                                         se.DeletedAt == null &&
                                         se.StartTime < endUtc &&
                                         se.EndTime > startUtc &&
                                         se.EntryType == EntryType.BLOCK, // filter BLOCK
                            includes: includes
                        );
                    }
                }
                else
                {
                    throw new ArgumentException($"Loại lịch (entryType) '{entryType}' không hợp lệ. Chỉ chấp nhận 'LESSON' hoặc 'BLOCK'.");
                }
            }
            else // no entryType filter, get all
            {
                scheduleEntries = await _uow.ScheduleEntries.GetAllAsync(
                    filter: baseFilter,
                    includes: includes
                );
            }

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
                Title = se.EntryType == EntryType.LESSON ? se.Lesson?.Title : "Lịch rảnh"
            });
        }

        public async Task<IEnumerable<ScheduleEntryDto>> GetStudentScheduleAsync(string studentUserId, DateTime startDate, DateTime endDate)
        {
            // take studentUserId, get StudentProfileId
            var studentProfileId = await _studentProfileService.GetStudentProfileIdByUserIdAsync(studentUserId);
            if (studentProfileId == null)
                throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            // change startDate, endDate to UTC, endDate is full day
            var startUtc = startDate.Date.ToUniversalTime();
            var endUtc = endDate.Date.AddDays(1).ToUniversalTime();

            // qyuery ScheduleEntries
            var scheduleEntries = await _uow.ScheduleEntries.GetAllAsync(
                filter: se =>
                    se.DeletedAt == null &&
                    se.EntryType == EntryType.LESSON && // student only see LESSON
                    se.StartTime < endUtc &&
                    se.EndTime > startUtc &&
                    // check if the Lesson's Class has an assignment for this student
                    se.Lesson.Class.ClassAssigns.Any(ca => ca.StudentId == studentProfileId),

                includes: query => query
                    .Include(se => se.Lesson)
                        .ThenInclude(l => l.Class)
                            .ThenInclude(c => c.ClassAssigns)
                    .OrderBy(se => se.StartTime)
            );

            // map to DTO
            return scheduleEntries.Select(se => new ScheduleEntryDto
            {
                Id = se.Id,
                TutorId = se.TutorId,
                StartTime = se.StartTime,
                EndTime = se.EndTime,
                EntryType = se.EntryType,
                LessonId = se.LessonId,
                ClassId = se.Lesson?.ClassId, // take from Lesson
                Title = se.Lesson?.Title      // take from Lesson
            });
        }
    }

}
