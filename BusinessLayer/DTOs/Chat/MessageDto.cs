using System;

namespace BusinessLayer.DTOs.Chat
{
    public class MessageDto
    {
        public string Id { get; set; } = default!;
        public string SenderId { get; set; } = default!;
        public string? SenderName { get; set; }
        public string? SenderAvatarUrl { get; set; }
        public string ReceiverId { get; set; } = default!;
        public string? ReceiverName { get; set; }
        public string? ReceiverAvatarUrl { get; set; }
        public string Content { get; set; } = default!;
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsRead => Status == "Read";
    }
}

