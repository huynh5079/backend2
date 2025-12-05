using BusinessLayer.DTOs.Schedule.Lesson;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Repositories.Abstraction.Schedule;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class LessonService : ILessonService
    {
        private readonly IScheduleUnitOfWork _uow;

        public LessonService(IScheduleUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<IEnumerable<ClassLessonDto>> GetLessonsByClassIdAsync(string classId)
        {
            var lessons = await _uow.Lessons.GetAllAsync(
                filter: l => l.ClassId == classId && l.DeletedAt == null,
                includes: q => q.Include(l => l.Class)
                                .ThenInclude(c => c.Tutor)
                                .ThenInclude(t => t.User)
            );

            return lessons.Select(l => new ClassLessonDto
            {
                Id = l.Id,
                Title = l.Title,
                Status = (DataLayer.Enum.ClassStatus)l.Status,
                // Null coalescing to handle potential nulls
                TutorName = l.Class?.Tutor?.User?.UserName ?? "N/A",
                TutorId = l.Class?.TutorId ?? ""
            });
        }

        public async Task<LessonDetailDto?> GetLessonDetailAsync(string lessonId)
        {
            var lesson = await _uow.Lessons.GetAsync(
                filter: l => l.Id == lessonId,
                includes: q => q.Include(l => l.Class)
                                .ThenInclude(c => c.Tutor)
                                .ThenInclude(t => t.User)
            );

            if (lesson == null) return null;

            var scheduleEntry = await _uow.ScheduleEntries.GetAsync(
                filter: s => s.LessonId == lessonId && s.DeletedAt == null
            );

            return new LessonDetailDto
            {
                Id = lesson.Id,
                LessonTitle = lesson.Title ?? "Buổi học không tên",
                Status = lesson.Status,

                // Time from ScheduleEntry
                StartTime = scheduleEntry?.StartTime ?? DateTime.MinValue,
                EndTime = scheduleEntry?.EndTime ?? DateTime.MinValue,

                // Class info
                ClassTitle = lesson.Class?.Title ?? "N/A",
                Subject = lesson.Class?.Subject ?? "N/A",
                EducationLevel = lesson.Class?.EducationLevel ?? "N/A",

                // Tutor info
                TutorName = lesson.Class?.Tutor?.User?.UserName ?? "N/A"
            };
        }
    }
}
