using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IMessageRepository : IGenericRepository<Message>
    {
        /// <summary>
        /// Lấy lịch sử chat giữa 2 user (phân trang)
        /// </summary>
        Task<PaginationResult<Message>> GetConversationAsync(string userId1, string userId2, int pageNumber, int pageSize);

        /// <summary>
        /// Lấy danh sách các cuộc trò chuyện của user (người đã chat với user này)
        /// Trả về danh sách user khác + tin nhắn cuối cùng
        /// </summary>
        Task<IReadOnlyList<(User otherUser, Message lastMessage, int unreadCount)>> GetConversationsAsync(string userId);

        /// <summary>
        /// Đếm số tin nhắn chưa đọc của user
        /// </summary>
        Task<int> GetUnreadCountAsync(string userId);

        /// <summary>
        /// Đánh dấu các tin nhắn là đã đọc
        /// </summary>
        Task MarkAsReadAsync(string userId, IEnumerable<string> messageIds);

        /// <summary>
        /// Đánh dấu tất cả tin nhắn từ một user cụ thể là đã đọc
        /// </summary>
        Task MarkConversationAsReadAsync(string userId, string otherUserId);

        /// <summary>
        /// Lấy tin nhắn theo ConversationId (phân trang)
        /// </summary>
        Task<PaginationResult<Message>> GetMessagesByConversationIdAsync(string conversationId, int pageNumber, int pageSize);

        /// <summary>
        /// Đánh dấu tất cả tin nhắn trong conversation là đã đọc
        /// </summary>
        Task MarkConversationMessagesAsReadAsync(string conversationId, string userId);
    }
}

