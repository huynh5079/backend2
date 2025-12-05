using DataLayer.Enum;
using System;

namespace BusinessLayer.DTOs.Chat
{
    public class MessageDto
    {
        public string Id { get; set; } = default!;
        public string SenderId { get; set; } = default!;
        public string? SenderName { get; set; }
        public string? SenderAvatarUrl { get; set; }
        public string? ReceiverId { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverAvatarUrl { get; set; }
        public string? ConversationId { get; set; }
        public string? Content { get; set; }
        public MessageType MessageType { get; set; } = MessageType.Text;
        
        // File/Image properties
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public string? MediaType { get; set; }
        public long? FileSize { get; set; }
        
        public string? Status { get; set; }
        public bool IsRead => Status == "Read";
        public bool IsEdited { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

