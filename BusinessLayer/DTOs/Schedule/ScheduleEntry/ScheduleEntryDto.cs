using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ScheduleEntry
{
    public class ScheduleEntryDto
    {
        public string Id { get; set; } = null!;
        public string TutorId { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public EntryType EntryType { get; set; } // "LESSON" or "BLOCK"

        // --- student view ---
        public string? LessonId { get; set; }
        public string? ClassId { get; set; }
        public string? Title { get; set; }
    }
}
