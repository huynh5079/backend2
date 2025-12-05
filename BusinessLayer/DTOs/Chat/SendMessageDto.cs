using DataLayer.Enum;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.DTOs.Chat
{
    public class SendMessageDto
    {
        // Cho chat 1-1 (backward compatible)
        public string? ReceiverId { get; set; }
        
        // Cho conversation-based chat
        public string? ConversationId { get; set; }
        
        public string? Content { get; set; }
        
        // File upload (optional)
        public IFormFile? File { get; set; }
    }

    public class EditMessageDto
    {
        public string Content { get; set; } = default!;
    }

    public class DeleteMessageDto
    {
        public bool DeleteForEveryone { get; set; } = false; // true: xóa cho tất cả, false: chỉ xóa cho mình
    }
}

