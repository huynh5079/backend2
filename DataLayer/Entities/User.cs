using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class User : BaseEntity
{
    // public string UserId { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; } = default!;

    public string? PasswordHash { get; set; } = default!;

    public string? Phone { get; set; }

    public string RoleId { get; set; }

    public string RoleName { get; set; }

    // Thêm để hỗ trợ Google
    public string? GoogleId { get; set; }     // sub từ Google (Unique, nullable)
    public string? AvatarUrl { get; set; }

    public AccountStatus Status { get; set; }

    public bool IsBanned { get; set; } = false;

    public DateTime? BannedAt { get; set; }

    public DateTime? BannedUntil { get; set; }  // optional: ban có thời hạn

    public string? BannedReason { get; set; }

    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public virtual ICollection<Feedback> FeedbackFromUsers { get; set; } = new List<Feedback>();

    public virtual ICollection<Feedback> FeedbackToUsers { get; set; } = new List<Feedback>();

    public virtual ICollection<Message> MessageReceivers { get; set; } = new List<Message>();

    public virtual ICollection<Message> MessageSenders { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<ParentProfile> ParentProfiles { get; set; } = new List<ParentProfile>();

    public virtual ICollection<Report> ReportReporters { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportTargetUsers { get; set; } = new List<Report>();

    public virtual Role Role { get; set; }

    public virtual ICollection<StudentProfile> StudentProfiles { get; set; } = new List<StudentProfile>();

    public virtual ICollection<TutorProfile> TutorProfiles { get; set; } = new List<TutorProfile>();

    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
