using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.Lesson
{
    public class TutorLessonDetailDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public LessonStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Class information
        public string ClassId { get; set; }
        public string ClassTitle { get; set; }
        public ClassMode Mode { get; set; } // "Online" hoặc "Offline"
        public string Subject { get; set; }
        public string EducationLevel { get; set; }
        public string Location { get; set; }
        public string OnlineStudyLink { get; set; }
        public string TutorUserId { get; set; }

        // Students in the lesson roster
        public List<LessonRosterItemDto> Students { get; set; } = new List<LessonRosterItemDto>();
    }

    // DTO for each student in the lesson roster
    public class LessonRosterItemDto
    {
        public string StudentId { get; set; } // StudentProfileId
        public string StudentUserId { get; set; }
        public string FullName { get; set; }
        public string AvatarUrl { get; set; }

        // Attendance information
        public bool IsPresent { get; set; }
        public AttendanceStatus? AttendanceStatus { get; set; } // "Present", "Absent", "Pending"...
        public string? Note { get; set; }
    }
}
