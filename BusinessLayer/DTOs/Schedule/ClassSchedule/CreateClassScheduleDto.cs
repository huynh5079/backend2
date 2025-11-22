using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ClassSchedule
{
    public class CreateClassScheduleDto
    {
        [Required(ErrorMessage = "Tiêu đề lớp học là bắt buộc.")]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        // --- SỬA CÁC TRƯỜNG LỖI THỜI ---
        [Required(ErrorMessage = "Môn học là bắt buộc.")]
        public string Subject { get; set; } = null!; // Sửa: Dùng string

        [Required(ErrorMessage = "Trình độ (Education Level) là bắt buộc.")]
        public string EducationLevel { get; set; } = null!; // Sửa: Dùng string

        [Required(ErrorMessage = "Giá tiền (Price) là bắt buộc.")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá tiền không thể âm.")]
        public decimal Price { get; set; }

        // --- THÊM CÁC TRƯỜNG MỚI ---
        [Required(ErrorMessage = "Hình thức học (Mode) là bắt buộc.")]
        public string Mode { get; set; } // "Online" hoặc "Offline"

        [Required(ErrorMessage = "Địa điểm (Location) là bắt buộc.")]
        public string Location { get; set; } // (Nếu Online, có thể gán "N/A")

        [Required(ErrorMessage = "Giới hạn học sinh (StudentLimit) là bắt buộc.")]
        [Range(1, 100, ErrorMessage = "Giới hạn học sinh phải từ 1.")]
        public int StudentLimit { get; set; }

        public DateTime? ClassStartDate { get; set; } // Ngày dự kiến bắt đầu
        public string? OnlineStudyLink { get; set; }

        // --- (Giữ nguyên) ---
        [Required]
        [MinLength(1, ErrorMessage = "Phải có ít nhất một lịch học lặp lại.")]
        public List<RecurringScheduleRuleDto> ScheduleRules { get; set; } = new List<RecurringScheduleRuleDto>();
    }

    public class RecurringScheduleRuleDto
    {
        [Required(ErrorMessage = "Ngày trong tuần là bắt buộc.")]
        public DayOfWeek DayOfWeek { get; set; }

        [Required(ErrorMessage = "Giờ bắt đầu là bắt buộc.")]
        public TimeSpan StartTime { get; set; } // Dùng TimeOnly

        [Required(ErrorMessage = "Giờ kết thúc là bắt buộc.")]
        public TimeSpan EndTime { get; set; }
    }
}