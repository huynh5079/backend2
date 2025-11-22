using System;
using System.Collections.Generic;
using DataLayer.Enum;

namespace DataLayer.Entities;

public partial class Notification : BaseEntity
{
    public string? UserId { get; set; }

    public string? Title { get; set; }

    public string? Message { get; set; }

    public NotificationType Type { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;

    public string? RelatedEntityId { get; set; } // ID của Transaction, Escrow, etc.

    public virtual User? User { get; set; }
}
