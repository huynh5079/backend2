using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class ParentProfile : BaseEntity
{
    // public string ParentId { get; set; }

    public string? UserId { get; set; }

    public string? LinkedStudentId { get; set; }

    public string? Relationship { get; set; }

    public virtual StudentProfile? LinkedStudent { get; set; }

    public virtual User? User { get; set; }
}
