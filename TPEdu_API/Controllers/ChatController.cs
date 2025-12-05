using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/chats")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IConversationService _conversationService;

        public ChatController(IChatService chatService, IConversationService conversationService)
        {
            _chatService = chatService;
            _conversationService = conversationService;
        }

        /// <summary>
        /// Gửi tin nhắn đến một user (hỗ trợ file/hình ảnh)
        /// </summary>
        [HttpPost("messages")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50_000_000)] // 50 MB limit
        public async Task<IActionResult> SendMessage([FromForm] SendMessageDto dto)
        {
            var senderId = User.RequireUserId();
            var message = await _chatService.SendMessageWithFileAsync(senderId, dto, dto.File, HttpContext.RequestAborted);
            
            return Ok(ApiResponse<MessageDto>.Ok(message, "Gửi tin nhắn thành công"));
        }

        /// <summary>
        /// Gửi tin nhắn đến một user (backward compatible - text only)
        /// </summary>
        [HttpPost("{receiverId}/messages")]
        public async Task<IActionResult> SendMessageLegacy(string receiverId, [FromBody] SendMessageDto dto)
        {
            if (dto.ReceiverId != receiverId)
                return BadRequest(ApiResponse<object>.Fail("ReceiverId không khớp với URL"));

            var senderId = User.RequireUserId();
            var message = await _chatService.SendMessageAsync(senderId, receiverId, dto.Content ?? "");
            
            return Ok(ApiResponse<MessageDto>.Ok(message, "Gửi tin nhắn thành công"));
        }

        /// <summary>
        /// Lấy tin nhắn theo ConversationId
        /// </summary>
        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<IActionResult> GetMessagesInConversation(
            string conversationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userId = User.RequireUserId();
            var rs = await _chatService.GetMessagesByConversationIdAsync(conversationId, userId, page, pageSize);
            
            return Ok(ApiResponse<object>.Ok(new 
            { 
                items = rs.Data, 
                page = rs.PageNumber, 
                size = rs.PageSize, 
                total = rs.TotalCount 
            }, "Lấy lịch sử chat thành công"));
        }

        /// <summary>
        /// Sửa tin nhắn
        /// </summary>
        [HttpPut("messages/{messageId}")]
        public async Task<IActionResult> EditMessage(string messageId, [FromBody] EditMessageDto dto)
        {
            var userId = User.RequireUserId();
            var message = await _chatService.EditMessageAsync(messageId, userId, dto.Content);
            
            return Ok(ApiResponse<MessageDto>.Ok(message, "Sửa tin nhắn thành công"));
        }

        /// <summary>
        /// Xóa tin nhắn
        /// </summary>
        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(string messageId, [FromBody] DeleteMessageDto? dto)
        {
            var userId = User.RequireUserId();
            var deleted = await _chatService.DeleteMessageAsync(messageId, userId, dto?.DeleteForEveryone ?? false);
            
            return Ok(ApiResponse<object>.Ok(new { deleted }, "Xóa tin nhắn thành công"));
        }

        /// <summary>
        /// Đánh dấu các tin nhắn là đã đọc
        /// </summary>
        [HttpPut("messages/read")]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkMessagesReadRequest request)
        {
            var userId = User.RequireUserId();
            await _chatService.MarkAsReadAsync(userId, request.MessageIds);
            
            return Ok(ApiResponse<object>.Ok(null, "Đánh dấu đã đọc thành công"));
        }

        /// <summary>
        /// Đánh dấu tất cả tin nhắn từ một user cụ thể là đã đọc
        /// </summary>
        [HttpPut("{otherUserId}/read")]
        public async Task<IActionResult> MarkConversationAsRead(string otherUserId)
        {
            var userId = User.RequireUserId();
            await _chatService.MarkConversationAsReadAsync(userId, otherUserId);
            
            return Ok(ApiResponse<object>.Ok(null, "Đánh dấu cuộc trò chuyện đã đọc thành công"));
        }

        /// <summary>
        /// Lấy số tin nhắn chưa đọc
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.RequireUserId();
            var count = await _chatService.GetUnreadCountAsync(userId);
            
            return Ok(ApiResponse<object>.Ok(new { count }, "Lấy số tin nhắn chưa đọc thành công"));
        }

        // ===== Conversation Endpoints =====

        /// <summary>
        /// Lấy hoặc tạo conversation 1-1
        /// </summary>
        [HttpGet("conversations/one-to-one/{otherUserId}")]
        public async Task<IActionResult> GetOrCreateOneToOneConversation(string otherUserId)
        {
            var userId = User.RequireUserId();
            var conversation = await _conversationService.GetOrCreateOneToOneConversationAsync(userId, otherUserId);
            
            return Ok(ApiResponse<ConversationDto>.Ok(conversation, "Lấy conversation thành công"));
        }

        /// <summary>
        /// Lấy hoặc tạo conversation cho lớp học
        /// </summary>
        [HttpGet("conversations/class/{classId}")]
        public async Task<IActionResult> GetOrCreateClassConversation(string classId)
        {
            var userId = User.RequireUserId();
            var conversation = await _conversationService.GetOrCreateClassConversationAsync(classId, userId);
            
            return Ok(ApiResponse<ConversationDto>.Ok(conversation, "Lấy conversation thành công"));
        }

        /// <summary>
        /// Lấy hoặc tạo conversation cho ClassRequest
        /// </summary>
        [HttpGet("conversations/class-request/{classRequestId}")]
        public async Task<IActionResult> GetOrCreateClassRequestConversation(string classRequestId)
        {
            var userId = User.RequireUserId();
            var conversation = await _conversationService.GetOrCreateClassRequestConversationAsync(classRequestId, userId);
            
            return Ok(ApiResponse<ConversationDto>.Ok(conversation, "Lấy conversation thành công"));
        }

        /// <summary>
        /// Lấy tất cả conversations của user
        /// </summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = User.RequireUserId();
            var conversations = await _conversationService.GetUserConversationsAsync(userId);
            
            return Ok(ApiResponse<object>.Ok(conversations, "Lấy danh sách conversation thành công"));
        }

        /// <summary>
        /// Lấy conversation theo ID
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        public async Task<IActionResult> GetConversationById(string conversationId)
        {
            var userId = User.RequireUserId();
            var conversation = await _conversationService.GetConversationByIdAsync(conversationId, userId);
            
            if (conversation == null)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy conversation"));
            
            return Ok(ApiResponse<ConversationDto>.Ok(conversation, "Lấy conversation thành công"));
        }
    }

    public class MarkMessagesReadRequest
    {
        public string[] MessageIds { get; set; } = Array.Empty<string>();
    }
}

