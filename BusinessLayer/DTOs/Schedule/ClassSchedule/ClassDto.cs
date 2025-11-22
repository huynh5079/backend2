using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ClassSchedule
{
    public class ClassDto
    {
        public string Id { get; set; } = null!;
        public string? TutorId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Subject { get; set; }
        public string? EducationLevel { get; set; }
        public decimal? Price { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // --- New attribute---
        public string? Location { get; set; }
        public int CurrentStudentCount { get; set; }
        public int StudentLimit { get; set; }
        public string? Mode { get; set; }
        public DateTime? ClassStartDate { get; set; }
        public string? OnlineStudyLink { get; set; }

        // Return rules
        public List<RecurringScheduleRuleDto> ScheduleRules { get; set; } = new List<RecurringScheduleRuleDto>();
    }
}