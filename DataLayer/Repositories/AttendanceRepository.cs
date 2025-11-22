using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class AttendanceRepository : GenericRepository<Attendance>, IAttendanceRepository
    {
        public AttendanceRepository(TpeduContext context) : base(context) { }

        public async Task<Attendance?> FindAsync(string lessonId, string studentId)
        {
            return await _context.Attendances
                .FirstOrDefaultAsync(a => a.LessonId == lessonId && a.StudentId == studentId);
        }

        public async Task<List<Attendance>> GetByLessonAsync(string lessonId)
        {
            return await _context.Attendances
                .Include(a => a.Student)
                .ThenInclude(s => s.User)
                .Where(a => a.LessonId == lessonId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Attendance>> GetByStudentRangeAsync(string studentId, DateTime fromUtc, DateTime toUtc)
        {
            // lấy theo Lesson in range qua ScheduleEntry
            var query = from se in _context.ScheduleEntries
                        join l in _context.Lessons on se.LessonId equals l.Id
                        join a in _context.Attendances
                            .Where(x => x.StudentId == studentId)
                            on l.Id equals a.LessonId into gj
                        from a in gj.DefaultIfEmpty()
                        where se.DeletedAt == null
                           && se.EntryType == EntryType.LESSON
                           && se.StartTime < toUtc
                           && se.EndTime > fromUtc
                        select a;

            return await query.Where(x => x != null).ToListAsync();
        }

        public async Task<List<(ScheduleEntry entry, Lesson lesson, Attendance? att)>> GetTutorScheduleAttendancesAsync(string tutorId, DateTime fromUtc, DateTime toUtc)
        {
            // Trả về từng (entry, lesson, attendance?) – attendance có thể null nếu chưa điểm danh
            var query = from se in _context.ScheduleEntries
                        where se.TutorId == tutorId
                           && se.EntryType == EntryType.LESSON
                           && se.DeletedAt == null
                           && se.StartTime < toUtc
                           && se.EndTime > fromUtc
                        join l in _context.Lessons on se.LessonId equals l.Id
                        // LEFT JOIN attendance, nhưng vì 1 lesson có nhiều HS,
                        // phần "attendance" sẽ aggregate ở Service; còn đây trả unit rỗng.
                        select new { entry = se, lesson = l };

            var rows = await query.ToListAsync();

            // Trả “att = null” tại đây; service sẽ load attendance theo group để tổng hợp
            return rows.Select(x => (x.entry, x.lesson, (Attendance?)null)).ToList();
        }
    }
}
