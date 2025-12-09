using BusinessLayer.DTOs.Schedule.Lesson;
using BusinessLayer.Service.Interface.IScheduleService;
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
    public class LessonService : ILessonService
    {
        private readonly IScheduleUnitOfWork _uow;
        private readonly IUnitOfWork _mainUow;

        public LessonService(IScheduleUnitOfWork uow, IUnitOfWork mainUow)
        {
            _uow = uow;
            _mainUow = mainUow;
        }

        #region Student & Parent
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
                ClassId = lesson.ClassId ?? "",
                ClassTitle = lesson.Class?.Title ?? "N/A",
                Subject = lesson.Class?.Subject ?? "N/A",
                EducationLevel = lesson.Class?.EducationLevel ?? "N/A",
                Mode = lesson.Class?.Mode ?? ClassMode.Offline,
                Location = lesson.Class?.Location ?? "N/A",
                OnlineStudyLink = lesson.Class?.OnlineStudyLink ?? "",

                // Tutor info
                TutorName = lesson.Class?.Tutor?.User?.UserName ?? "N/A",
                TutorUserId = lesson.Class?.Tutor?.UserId ?? ""
            };
        }
        #endregion

        #region Tutor
        public async Task<TutorLessonDetailDto?> GetTutorLessonDetailAsync(string lessonId, string tutorUserId)
        {
            // _uow for Lesson & Class
            var lesson = await _uow.Lessons.GetAsync(
                filter: l => l.Id == lessonId && l.DeletedAt == null,
                includes: q => q.Include(l => l.Class)
            );

            if (lesson == null) return null;

            // Validate Tutor
            var classEntity = await _uow.Classes.GetAsync(
                filter: c => c.Id == lesson.ClassId,
                includes: i => i.Include(c => c.Tutor)
            );

            // check tutor ownership
            if (classEntity == null || classEntity.Tutor?.UserId != tutorUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập buổi học này.");

            // Class time from ScheduleEntry
            var scheduleEntry = await _uow.ScheduleEntries.GetAsync(
                filter: s => s.LessonId == lessonId && s.DeletedAt == null
            );

            // Students enrolled in the class
            var enrolledStudents = await _uow.ClassAssigns.GetAllAsync(
                filter: ca => ca.ClassId == lesson.ClassId
                              && ca.ApprovalStatus == ApprovalStatus.Approved
                              && ca.DeletedAt == null,
                includes: q => q.Include(ca => ca.Student).ThenInclude(s => s.User)
            );

            // _mainUow for Attendance records
            var existingAttendances = await _mainUow.Attendances.GetAllAsync(
                filter: a => a.LessonId == lessonId && a.DeletedAt == null
            );

            // mapping
            var roster = enrolledStudents.Select(assign => {
                var attendanceRecord = existingAttendances.FirstOrDefault(a => a.StudentId == assign.StudentId);

                return new LessonRosterItemDto
                {
                    StudentId = assign.StudentId!,
                    StudentUserId = assign.Student?.UserId ?? "",
                    FullName = assign.Student?.User?.UserName ?? "N/A",
                    AvatarUrl = assign.Student?.User?.AvatarUrl ?? "",

                    // If no attendance record, default to Absent
                    AttendanceStatus = attendanceRecord?.Status,

                    // Logic phụ trợ IsPresent
                    IsPresent = attendanceRecord?.Status == AttendanceStatus.Present,
                    Note = attendanceRecord?.Notes
                };
            }).ToList();

            return new TutorLessonDetailDto
            {
                Id = lesson.Id,
                Title = lesson.Title ?? "Buổi học",
                Status = lesson.Status,
                StartTime = scheduleEntry?.StartTime ?? DateTime.MinValue,
                EndTime = scheduleEntry?.EndTime ?? DateTime.MinValue,
                ClassId = lesson.ClassId!,
                ClassTitle = classEntity.Title ?? "",
                Mode = classEntity.Mode,
                Subject = classEntity.Subject ?? "N/A",
                EducationLevel = classEntity.EducationLevel ?? "N/A",
                Location = classEntity.Location ?? "N/A",
                OnlineStudyLink = classEntity.OnlineStudyLink ?? "",
                TutorUserId = tutorUserId,
                Students = roster
            };
        }
        #endregion
    }
}
