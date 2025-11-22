using DataLayer.Entities;
using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Schedule.ClassRequest
{
    public class ClassRequestResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; }

        [StringLength(200, ErrorMessage = "Dịa chỉ là bắt buộc nếu học tại nhà")]
        public string? Location { get; set; }
        public string? SpecialRequirements { get; set; }
        public decimal Budget { get; set; }
        public string? OnlineStudyLink { get; set; }

        public ClassRequestStatus Status { get; set; }
        public ClassMode Mode { get; set; }
        public DateTime? ClassStartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // join info
        public string? StudentName { get; set; }
        public string? TutorName { get; set; }
        public string Subject { get; set; }
        public string EducationLevel { get; set; }

        // return schedules
        public List<ClassRequestScheduleDto> Schedules { get; set; } = new();
    }
}