using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Attendance
{
    public class MarkAttendanceRequest
    {
        public string LessonId { get; set; } = default!;
        public string StudentId { get; set; } = default!;
        public string Status { get; set; } = default!; // "Present"/"Late"/"Absent"/"Excused"
        public string? Notes { get; set; }
    }

    public class AttendanceRecordDto
    {
        public string LessonId { get; set; } = default!;
        public string StudentId { get; set; } = default!;
        public string StudentName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // Để overlay lên “lịch học”
    public class ScheduleCellAttendanceDto
    {
        public string ScheduleEntryId { get; set; } = default!;
        public string LessonId { get; set; } = default!;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        // Với lớp: tổng quát hoá
        public int TotalStudents { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public int Excused { get; set; }

        // Với 1-1: FE có thể nhìn vào tổng số và quyết định “chip/badge”
    }
}
