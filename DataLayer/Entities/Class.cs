using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Class : BaseEntity
{
    //public string ClassId { get; set; }

    public string? TutorId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public ClassStatus? Status { get; set; }

    public string? Location { get; set; }

    public int CurrentStudentCount { get; set; }

    public int StudentLimit { get; set; }

    public ClassMode Mode { get; set; }

    public string? Subject { get; set; }

    public string? EducationLevel { get; set; }

    public DateTime? ClassStartDate { get; set; }

    public string? OnlineStudyLink { get; set; }

    public virtual ICollection<ClassAssign> ClassAssigns { get; set; } = new List<ClassAssign>();

    public virtual ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();

    public virtual ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual TutorProfile? Tutor { get; set; }
}
