using BusinessLayer.DTOs.Attendance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IAttendanceService
    {
        // Tutor thao tác
        Task<AttendanceRecordDto> MarkAsync(string tutorUserId, MarkAttendanceRequest req);
        Task<List<AttendanceRecordDto>> BulkMarkForLessonAsync(string tutorUserId, string lessonId, Dictionary<string, string> studentStatusMap, string? notes = null);

        // Xem trên lịch (tutor)
        Task<IEnumerable<ScheduleCellAttendanceDto>> GetTutorAttendanceOnCalendarAsync(string tutorUserId, DateTime start, DateTime end);

        // Xem trên lịch (student/parent)
        Task<IEnumerable<ScheduleCellAttendanceDto>> GetStudentAttendanceOnCalendarAsync(string studentUserId, DateTime start, DateTime end);

        // Phụ huynh xem theo “con”
        Task<IEnumerable<ScheduleCellAttendanceDto>> GetParentChildAttendanceCalendarAsync(string parentUserId, string studentId, DateTime start, DateTime end);

        // Chi tiết 1 buổi (lesson) cho tutor
        Task<IEnumerable<AttendanceRecordDto>> GetLessonAttendanceAsync(string tutorUserId, string lessonId);

        Task<List<LessonRosterItemDto>> GetLessonRosterForTutorAsync(string tutorUserId, string lessonId);
    }
}
