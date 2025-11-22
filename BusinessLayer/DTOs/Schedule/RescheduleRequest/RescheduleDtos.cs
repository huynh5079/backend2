using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.RescheduleRequest
{
    public class CreateRescheduleRequestDto
    {
        [Required]
        public DateTime NewStartTime { get; set; }

        [Required]
        public DateTime NewEndTime { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class RescheduleRequestDto
    {
        public string Id { get; set; } = default!;
        public string LessonId { get; set; } = default!;
        public string RequesterUserId { get; set; } = default!;
        public string RequesterName { get; set; } = default!;

        public DateTime OldStartTime { get; set; }
        public DateTime OldEndTime { get; set; }
        public DateTime NewStartTime { get; set; }
        public DateTime NewEndTime { get; set; }

        public string? Reason { get; set; }
        public RescheduleStatus Status { get; set; }

        public string? ResponderUserId { get; set; }
        public string? ResponderName { get; set; }
        public DateTime? RespondedAt { get; set; }
    }
}
