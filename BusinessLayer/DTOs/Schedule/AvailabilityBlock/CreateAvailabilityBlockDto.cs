using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.AvailabilityBlock
{
    public class CreateAvailabilityBlockDto
    {
        // TutorId from user claims,
        // public string TutorId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public string? Notes { get; set; }

        public RecurrenceRuleDto? RecurrenceRule { get; set; } 
    }

    // DTO for recurrence rules
    public class RecurrenceRuleDto
    {
        public string Frequency { get; set; } // Weekly, Daily
        public List<DayOfWeek> DaysOfWeek { get; set; } // Tuesday, Friday
        public DateTime UntilDate { get; set; } // Lasting till date
    }
}
