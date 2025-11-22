using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
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

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Lấy danh sách các cuộc trò chuyện của user hiện tại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var userId = User.RequireUserId();
            var conversations = await _chatService.GetConversationsAsync(userId);
            return Ok(ApiResponse<object>.Ok(conversations, "Lấy danh sách cuộc trò chuyện thành công"));
        }

        /// <summary>
        /// Lấy lịch sử chat với một user cụ thể (phân trang)
        /// </summary>
        [HttpGet("{otherUserId}/messages")]
        public async Task<IActionResult> GetConversationMessages(
            string otherUserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userId = User.RequireUserId();
            var rs = await _chatService.GetConversationAsync(userId, otherUserId, page, pageSize);
            
            return Ok(ApiResponse<object>.Ok(new 
            { 
                items = rs.Data, 
                page = rs.PageNumber, 
                size = rs.PageSize, 
                total = rs.TotalCount 
            }, "Lấy lịch sử chat thành công"));
        }

        /// <summary>
        /// Gửi tin nhắn đến một user
        /// </summary>
        [HttpPost("{receiverId}/messages")]
        public async Task<IActionResult> SendMessage(string receiverId, [FromBody] SendMessageDto dto)
        {
            if (dto.ReceiverId != receiverId)
                return BadRequest(ApiResponse<object>.Fail("ReceiverId không khớp với URL"));

            var senderId = User.RequireUserId();
            var message = await _chatService.SendMessageAsync(senderId, receiverId, dto.Content);
            
            return Ok(ApiResponse<MessageDto>.Ok(message, "Gửi tin nhắn thành công"));
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
    }

    public class MarkMessagesReadRequest
    {
        public string[] MessageIds { get; set; } = Array.Empty<string>();
    }
}

