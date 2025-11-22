using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.Class
{
    public class ClassDto
    {
        public string Id { get; set; } = null!;
        public string TutorId { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Subject { get; set; }
        public string? EducationLevel { get; set; }
        public decimal Price { get; set; }
        public ClassStatus Status { get; set; } // e.g., "PENDING"
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // --- New attribute---

        [StringLength(200, ErrorMessage = "Dịa chỉ là bắt buộc nếu học tại nhà")]
        public string? Location { get; set; }
        public int CurrentStudentCount { get; set; }
        public int StudentLimit { get; set; }
        public string? Mode { get; set; }
        public DateTime? ClassStartDate { get; set; }
        public string? OnlineStudyLink { get; set; }

        // Trả về các quy tắc đã tạo
        public List<RecurringScheduleRuleDto> ScheduleRules { get; set; } = new List<RecurringScheduleRuleDto>();
    }
}