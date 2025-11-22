using DataLayer.Entities;
using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class ClassRequest : BaseEntity
{
    // public string RequestId { get; set; }

    public string? StudentId { get; set; }

    public string? TutorId { get; set; }

    public decimal? Budget { get; set; }

    public ClassRequestStatus Status { get; set; }

    public ClassMode Mode { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public string? SpecialRequirements { get; set; }

    public string? Subject { get; set; }

    public string? EducationLevel { get; set; }

    public DateTime? ClassStartDate { get; set; }

    public string? OnlineStudyLink { get; set; }

    public virtual ICollection<ClassRequestSchedule> ClassRequestSchedules { get; set; } = new List<ClassRequestSchedule>();

    public virtual StudentProfile? Student { get; set; }

    public virtual TutorProfile? Tutor { get; set; }

    public virtual ICollection<TutorApplication> TutorApplications { get; set; } = new List<TutorApplication>();
}
