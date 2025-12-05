using System;

namespace DataLayer.Entities;

public partial class FavoriteTutor : BaseEntity
{
    public string UserId { get; set; } = null!;

    public string TutorProfileId { get; set; } = null!;

    // Navigation properties
    public virtual User User { get; set; } = null!;

    public virtual TutorProfile TutorProfile { get; set; } = null!;
}
