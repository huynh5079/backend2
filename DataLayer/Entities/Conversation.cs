using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Conversation : BaseEntity
{
    public string Title { get; set; } = default!;
    public ConversationType Type { get; set; }
    
    // Cho chat 1-1: null
    // Cho chat theo lớp: ClassId
    public string? ClassId { get; set; }
    
    // Cho chat theo ClassRequest: ClassRequestId
    public string? ClassRequestId { get; set; }
    
    // Cho chat 1-1: UserId của 2 người (lưu trong ConversationParticipant)
    // Cho chat theo lớp: tất cả members (Tutor + Students)
    
    public DateTime LastMessageAt { get; set; }
    
    // Navigation properties
    public virtual Class? Class { get; set; }
    public virtual ClassRequest? ClassRequest { get; set; }
    public virtual ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}

