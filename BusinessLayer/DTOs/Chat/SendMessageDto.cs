namespace BusinessLayer.DTOs.Chat
{
    public class SendMessageDto
    {
        public string ReceiverId { get; set; } = default!;
        public string Content { get; set; } = default!;
    }
}

