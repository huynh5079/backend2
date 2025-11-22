using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Media : BaseEntity
{
    public string FileUrl { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string MediaType { get; set; } = default!;
    public long FileSize { get; set; }
    public string OwnerUserId { get; set; } = default!;
    public UploadContext Context { get; set; }

    public string? LessonId { get; set; }
    public string? TutorProfileId { get; set; }
    public string? ProviderPublicId { get; set; }

    public virtual User OwnerUser { get; set; } = default!;
    public virtual Lesson? Lesson { get; set; }
    public virtual TutorProfile? TutorProfile { get; set; }
}