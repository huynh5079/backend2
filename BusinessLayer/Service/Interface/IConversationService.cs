using BusinessLayer.DTOs.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IConversationService
    {
        /// <summary>
        /// Tạo hoặc lấy conversation 1-1 giữa 2 user
        /// </summary>
        Task<ConversationDto> GetOrCreateOneToOneConversationAsync(string userId1, string userId2);

        /// <summary>
        /// Tạo hoặc lấy conversation cho lớp học
        /// </summary>
        Task<ConversationDto> GetOrCreateClassConversationAsync(string classId, string userId);

        /// <summary>
        /// Tạo hoặc lấy conversation cho ClassRequest
        /// </summary>
        Task<ConversationDto> GetOrCreateClassRequestConversationAsync(string classRequestId, string userId);

        /// <summary>
        /// Lấy tất cả conversations của user
        /// </summary>
        Task<IReadOnlyList<ConversationDto>> GetUserConversationsAsync(string userId);

        /// <summary>
        /// Lấy conversation theo ID
        /// </summary>
        Task<ConversationDto?> GetConversationByIdAsync(string conversationId, string userId);
    }
}

