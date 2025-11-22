using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ClassRequest
{
    public class ClassRequestScheduleDto
    {
        [Required]
        [Range(0, 6, ErrorMessage = "DayOfWeek must be between 0 (Sunday) and 6 (Saturday)")]
        public byte DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; } // Ex: "08:00:00"

        [Required]
        public TimeSpan EndTime { get; set; }   // EX: "10:00:00"
    }
}
