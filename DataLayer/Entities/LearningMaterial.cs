using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class LearningMaterial : BaseEntity
{
    // public string MaterialId { get; set; }

    public string? ClassId { get; set; }

    public string? Title { get; set; }

    public string? FileUrl { get; set; }

    public string? Type { get; set; }

    public virtual Class? Class { get; set; }
}
