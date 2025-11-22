using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Report : BaseEntity
{
    // public string ReportId { get; set; }

    public string? ReporterId { get; set; }

    public string? TargetUserId { get; set; }

    public string? TargetLessonId { get; set; }

    public string? TargetMediaId { get; set; }

    public string? Description { get; set; }

    public ReportStatus Status { get; set; }

    public virtual User? Reporter { get; set; }

    public virtual Lesson? TargetLesson { get; set; }

    public virtual User? TargetUser { get; set; }

    public virtual Media? TargetMedia { get; set; }
}
