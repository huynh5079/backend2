using System;

namespace BusinessLayer.DTOs.Chat
{
    public class ConversationDto
    {
        public string OtherUserId { get; set; } = default!;
        public string? OtherUserName { get; set; }
        public string? OtherUserAvatarUrl { get; set; }
        public string? LastMessageContent { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }
}

