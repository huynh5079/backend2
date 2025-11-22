namespace BusinessLayer.DTOs.Class;

public class ClassResponseDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public string EducationLevelName { get; set; } = null!;
    public string TeachingFormat { get; set; } = null!;
    public string Address { get; set; } = null!;
    public int StudentLimit { get; set; }
    public int CurrentStudentCount { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<ScheduleSlotResponseDto> ScheduleSlots { get; set; } = new();
}

public class ScheduleSlotResponseDto
{
    public string DayOfWeek { get; set; } = null!;
    public string Session { get; set; } = null!;
    public string Slot { get; set; } = null!;
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
}
