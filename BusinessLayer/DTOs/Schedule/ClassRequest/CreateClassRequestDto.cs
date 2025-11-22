using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Schedule.ClassRequest
{
    public class CreateClassRequestDto
    {
        // directly student create, so no StudentId needed
        public string? TutorId { get; set; }

        [Required]
        public string Subject { get; set; }

        [Required]
        public string EducationLevel { get; set; }

        [Required]
        public string Description { get; set; }

        [StringLength(200, ErrorMessage = "Dịa chỉ là bắt buộc nếu học tại nhà")]
        public string? Location { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Budget { get; set; }

        [Required]
        public ClassMode Mode { get; set; } // "Online" hoặc "Offline"

        // start learning date
        public DateTime? ClassStartDate { get; set; }
        public string? OnlineStudyLink { get; set; }
        public string? SpecialRequirements { get; set; }

        // list expected schedules
        [Required]
        [MinLength(1, ErrorMessage = "Must have at least one schedule slot")]
        public List<ClassRequestScheduleDto> Schedules { get; set; } = new();
    }
}