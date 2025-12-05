using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace BusinessLayer.DTOs.Chat
{
    public class ConversationDto
    {
        public string Id { get; set; } = default!;
        public string Title { get; set; } = default!;
        public ConversationType Type { get; set; }
        
        // Cho chat 1-1
        public string? OtherUserId { get; set; }
        public string? OtherUserName { get; set; }
        public string? OtherUserAvatarUrl { get; set; }
        
        // Cho chat theo lớp
        public string? ClassId { get; set; }
        public string? ClassTitle { get; set; }
        
        // Cho chat theo ClassRequest
        public string? ClassRequestId { get; set; }
        
        public string? LastMessageContent { get; set; }
        public MessageType? LastMessageType { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
        
        // Participants (cho group chat)
        public List<ParticipantDto>? Participants { get; set; }
    }

    public class ParticipantDto
    {
        public string UserId { get; set; } = default!;
        public string? UserName { get; set; }
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = "Member";
    }
}

