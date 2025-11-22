using DataLayer.Entities;
using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class TutorApplication : BaseEntity
{
    //public string ApplicationId { get; set; } = null!;

    public string ClassRequestId { get; set; } = null!;

    public string TutorId { get; set; } = null!;

    public ApplicationStatus Status { get; set; }

    public DateTime AppliedAt { get; set; }

    public virtual ClassRequest ClassRequest { get; set; } = null!;

    public virtual TutorProfile Tutor { get; set; } = null!;
}
