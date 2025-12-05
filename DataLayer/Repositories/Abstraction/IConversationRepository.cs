using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IConversationRepository : IGenericRepository<Conversation>
    {
        Task<Conversation?> GetOneToOneConversationAsync(string userId1, string userId2);
        Task<Conversation?> GetClassConversationAsync(string classId);
        Task<Conversation?> GetClassRequestConversationAsync(string classRequestId);
        Task<Conversation?> GetByIdWithParticipantsAsync(string conversationId);
        Task<IReadOnlyList<Conversation>> GetUserConversationsAsync(string userId);
        Task<ConversationParticipant?> GetParticipantAsync(string conversationId, string userId);
        Task AddParticipantAsync(ConversationParticipant participant);
        Task RemoveParticipantAsync(string conversationId, string userId);
    }
}

