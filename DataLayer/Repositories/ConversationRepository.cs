using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class ConversationRepository : GenericRepository<Conversation>, IConversationRepository
    {
        public ConversationRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<Conversation?> GetOneToOneConversationAsync(string userId1, string userId2)
        {
            return await _dbSet
                .Include(c => c.Participants)
                .Where(c => c.Type == ConversationType.OneToOne &&
                           c.Participants.Any(p => p.UserId == userId1) &&
                           c.Participants.Any(p => p.UserId == userId2) &&
                           c.Participants.Count == 2)
                .FirstOrDefaultAsync();
        }

        public async Task<Conversation?> GetClassConversationAsync(string classId)
        {
            return await _dbSet
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .Where(c => c.Type == ConversationType.Class && c.ClassId == classId)
                .FirstOrDefaultAsync();
        }

        public async Task<Conversation?> GetClassRequestConversationAsync(string classRequestId)
        {
            return await _dbSet
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .Where(c => c.Type == ConversationType.ClassRequest && c.ClassRequestId == classRequestId)
                .FirstOrDefaultAsync();
        }

        public async Task<Conversation?> GetByIdWithParticipantsAsync(string conversationId)
        {
            return await _dbSet
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .Include(c => c.Class)
                .Include(c => c.ClassRequest)
                .Include(c => c.Messages.Where(m => m.DeletedAt == null))
                .ThenInclude(m => m.Sender)
                .Where(c => c.Id == conversationId)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<Conversation>> GetUserConversationsAsync(string userId)
        {
            return await _dbSet
                .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                .Where(c => c.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();
        }

        public async Task<ConversationParticipant?> GetParticipantAsync(string conversationId, string userId)
        {
            return await _context.Set<ConversationParticipant>()
                .Include(p => p.User)
                .Where(p => p.ConversationId == conversationId && p.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task AddParticipantAsync(ConversationParticipant participant)
        {
            await _context.Set<ConversationParticipant>().AddAsync(participant);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveParticipantAsync(string conversationId, string userId)
        {
            var participant = await GetParticipantAsync(conversationId, userId);
            if (participant != null)
            {
                _context.Set<ConversationParticipant>().Remove(participant);
                await _context.SaveChangesAsync();
            }
        }
    }
}

