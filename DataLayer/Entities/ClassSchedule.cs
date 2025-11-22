using System;

namespace DataLayer.Entities;

public partial class ClassSchedule : BaseEntity
{
    //public string Id { get; set; } = null!;

    public string ClassId { get; set; } = null!;

    public byte DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public virtual Class Class { get; set; } = null!;
}
