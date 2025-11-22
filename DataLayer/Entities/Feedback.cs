using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Feedback : BaseEntity
{
    // public string FeedbackId { get; set; }

    public string? FromUserId { get; set; }

    public string? ToUserId { get; set; }

    public string? LessonId { get; set; }

    public string? ClassId { get; set; }

    public float? Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsPublicOnTutorProfile { get; set; } = false;

    public virtual User? FromUser { get; set; }

    public virtual Lesson? Lesson { get; set; }

    public virtual User? ToUser { get; set; }

    public virtual Class? Class { get; set; }
}
