namespace BusinessLayer.DTOs.Notification;

public class NotificationDto
{
    public string Id { get; set; } = default!;
    public string? UserId { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? RelatedEntityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MarkAsReadRequest
{
    public string NotificationId { get; set; } = default!;
}

