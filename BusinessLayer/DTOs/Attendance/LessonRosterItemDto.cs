using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Attendance
{
    public class LessonRosterItemDto
    {
        public string StudentId { get; set; } = default!;       // Id của StudentProfile
        public string StudentUserId { get; set; } = default!;
        public string StudentName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? AvatarUrl { get; set; }
        public string? CurrentStatus { get; set; }               // null nếu chưa điểm danh
    }
}
