using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class MessageRepository : GenericRepository<Message>, IMessageRepository
    {
        public MessageRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<PaginationResult<Message>> GetConversationAsync(string userId1, string userId2, int pageNumber, int pageSize)
        {
            IQueryable<Message> query = _dbSet
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                           (m.SenderId == userId2 && m.ReceiverId == userId1))
                .OrderByDescending(m => m.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginationResult<Message>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<IReadOnlyList<(User otherUser, Message lastMessage, int unreadCount)>> GetConversationsAsync(string userId)
        {
            // Lấy tất cả tin nhắn liên quan đến user này (gửi hoặc nhận)
            var messages = await _dbSet
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            // Nhóm theo user khác (người đã chat)
            var conversationDict = new Dictionary<string, (User otherUser, Message lastMessage, int unreadCount)>();

            foreach (var msg in messages)
            {
                // Xác định user khác
                string otherUserId = msg.SenderId == userId ? msg.ReceiverId! : msg.SenderId!;
                User? otherUser = msg.SenderId == userId ? msg.Receiver : msg.Sender;

                if (otherUser == null || string.IsNullOrWhiteSpace(otherUserId))
                    continue;

                // Nếu chưa có trong dict hoặc tin nhắn này mới hơn
                if (!conversationDict.ContainsKey(otherUserId))
                {
                    // Đếm unread (chỉ tin nhắn user này nhận được và chưa đọc)
                    int unread = await _dbSet
                        .CountAsync(m => m.SenderId == otherUserId &&
                                        m.ReceiverId == userId &&
                                        (m.Status == null || m.Status != "Read"));

                    conversationDict[otherUserId] = (otherUser, msg, unread);
                }
            }

            // Sắp xếp theo thời gian tin nhắn cuối (mới nhất trước)
            return conversationDict.Values
                .OrderByDescending(x => x.lastMessage.CreatedAt)
                .ToList();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _dbSet
                .CountAsync(m => m.ReceiverId == userId &&
                                (m.Status == null || m.Status != "Read"));
        }

        public async Task MarkAsReadAsync(string userId, IEnumerable<string> messageIds)
        {
            var messages = await _dbSet
                .Where(m => messageIds.Contains(m.Id) && m.ReceiverId == userId)
                .ToListAsync();

            foreach (var msg in messages)
            {
                msg.Status = "Read";
            }

            await _context.SaveChangesAsync();
        }

        public async Task MarkConversationAsReadAsync(string userId, string otherUserId)
        {
            var messages = await _dbSet
                .Where(m => m.SenderId == otherUserId &&
                           m.ReceiverId == userId &&
                           (m.Status == null || m.Status != "Read"))
                .ToListAsync();

            foreach (var msg in messages)
            {
                msg.Status = "Read";
            }

            await _context.SaveChangesAsync();
        }
    }
}

