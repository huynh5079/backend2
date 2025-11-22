using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Class;

public class CreateClassDto
{
    [Required(ErrorMessage = "Môn học là bắt buộc")]
    public string SubjectId { get; set; } = null!;

    [Required(ErrorMessage = "Cấp lớp là bắt buộc")]
    public string EducationLevelId { get; set; } = null!;

    [Required(ErrorMessage = "Hình thức học là bắt buộc")]
    public string TeachingFormat { get; set; } = null!;

    [Required(ErrorMessage = "Địa chỉ là bắt buộc")]
    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string Address { get; set; } = null!;

    [Required(ErrorMessage = "Số lượng học viên giới hạn là bắt buộc")]
    [Range(1, 50, ErrorMessage = "Số lượng học viên phải từ 1 đến 50")]
    public int StudentLimit { get; set; }

    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Lịch dạy là bắt buộc")]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 khung giờ dạy")]
    public List<ScheduleSlotDto> ScheduleSlots { get; set; } = new();
}

public class ScheduleSlotDto
{
    [Required(ErrorMessage = "Ngày trong tuần là bắt buộc")]
    public string DayOfWeek { get; set; } = null!;

    [Required(ErrorMessage = "Ca học là bắt buộc")]
    public string Session { get; set; } = null!;

    [Required(ErrorMessage = "Slot là bắt buộc")]
    public string Slot { get; set; } = null!;

    [Required(ErrorMessage = "Thời gian bắt đầu là bắt buộc")]
    public string StartTime { get; set; } = null!;

    [Required(ErrorMessage = "Thời gian kết thúc là bắt buộc")]
    public string EndTime { get; set; } = null!;
}
