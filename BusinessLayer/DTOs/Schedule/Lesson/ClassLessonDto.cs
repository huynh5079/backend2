using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.Lesson
{
    // Lesson list
    public class ClassLessonDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public ClassStatus Status { get; set; }
        public string TutorName { get; set; }
        public string TutorId { get; set; }
    }

    // Lesson detail
    public class LessonDetailDto
    {
        public string Id { get; set; }
        public string LessonTitle { get; set; }
        public LessonStatus Status { get; set; }

        // ScheduleEntry
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Class information
        public string ClassTitle { get; set; }
        public string Subject { get; set; }
        public string EducationLevel { get; set; }

        // tutor information
        public string TutorName { get; set; }
    }
}
