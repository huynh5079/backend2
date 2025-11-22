using BusinessLayer.DTOs.Chat;
using DataLayer.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IChatService
    {
        /// <summary>
        /// Gửi tin nhắn từ sender đến receiver
        /// </summary>
        Task<MessageDto> SendMessageAsync(string senderId, string receiverId, string content);

        /// <summary>
        /// Lấy lịch sử chat giữa 2 user (phân trang)
        /// </summary>
        Task<PaginationResult<MessageDto>> GetConversationAsync(string userId1, string userId2, int page, int pageSize);

        /// <summary>
        /// Lấy danh sách các cuộc trò chuyện của user
        /// </summary>
        Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string userId);

        /// <summary>
        /// Đánh dấu các tin nhắn là đã đọc
        /// </summary>
        Task MarkAsReadAsync(string userId, IEnumerable<string> messageIds);

        /// <summary>
        /// Đánh dấu tất cả tin nhắn từ một user cụ thể là đã đọc
        /// </summary>
        Task MarkConversationAsReadAsync(string userId, string otherUserId);

        /// <summary>
        /// Lấy số tin nhắn chưa đọc
        /// </summary>
        Task<int> GetUnreadCountAsync(string userId);
    }
}

