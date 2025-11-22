using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class StudentProfile : BaseEntity
{
    // public string StudentId { get; set; }

    public string? UserId { get; set; }

    public string? PreferredSubjects { get; set; }

    public string? EducationLevel { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual ICollection<ClassAssign> ClassAssigns { get; set; } = new List<ClassAssign>();

    public virtual ICollection<ClassRequest> ClassRequests { get; set; } = new List<ClassRequest>();

    public virtual ICollection<ParentProfile> ParentProfiles { get; set; } = new List<ParentProfile>();

    public virtual User? User { get; set; }
}
