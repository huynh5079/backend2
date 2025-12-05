using System;

namespace DataLayer.Entities;

public partial class ConversationParticipant : BaseEntity
{
    public string ConversationId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    
    // Role trong conversation (Owner, Member, Admin)
    public string Role { get; set; } = "Member";
    
    // Thời gian tham gia
    public DateTime JoinedAt { get; set; } = DateTime.Now;
    
    // Số tin nhắn chưa đọc trong conversation này
    public int UnreadCount { get; set; } = 0;
    
    // Navigation properties
    public virtual Conversation Conversation { get; set; } = default!;
    public virtual User User { get; set; } = default!;
}

