using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _uow;
        private readonly IChatHubService _hubService;

        public ChatService(IUnitOfWork uow, IChatHubService hubService)
        {
            _uow = uow;
            _hubService = hubService;
        }

        public async Task<MessageDto> SendMessageAsync(string senderId, string receiverId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Nội dung tin nhắn không được để trống");

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                Status = "Sent",
                CreatedAt = DateTime.Now
            };

            await _uow.Messages.CreateAsync(message);
            await _uow.SaveChangesAsync();

            // Map to DTO
            var sender = await _uow.Users.GetByIdAsync(senderId);
            var receiver = await _uow.Users.GetByIdAsync(receiverId);

            var messageDto = new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId!,
                SenderName = sender?.UserName,
                SenderAvatarUrl = sender?.AvatarUrl,
                ReceiverId = message.ReceiverId!,
                ReceiverName = receiver?.UserName,
                ReceiverAvatarUrl = receiver?.AvatarUrl,
                Content = message.Content!,
                Status = message.Status,
                CreatedAt = message.CreatedAt
            };

            // Push realtime qua ChatHub
            await _hubService.SendMessageToUserAsync(receiverId, messageDto);

            return messageDto;
        }

        public async Task<PaginationResult<MessageDto>> GetConversationAsync(string userId1, string userId2, int page, int pageSize)
        {
            var rs = await _uow.Messages.GetConversationAsync(userId1, userId2, page, pageSize);

            var messageDtos = rs.Data.Select(m => new MessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId!,
                SenderName = m.Sender?.UserName,
                SenderAvatarUrl = m.Sender?.AvatarUrl,
                ReceiverId = m.ReceiverId!,
                ReceiverName = m.Receiver?.UserName,
                ReceiverAvatarUrl = m.Receiver?.AvatarUrl,
                Content = m.Content!,
                Status = m.Status,
                CreatedAt = m.CreatedAt
            }).ToList();

            return new PaginationResult<MessageDto>(messageDtos, rs.TotalCount, rs.PageNumber, rs.PageSize);
        }

        public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(string userId)
        {
            var conversations = await _uow.Messages.GetConversationsAsync(userId);

            return conversations.Select(c => new ConversationDto
            {
                OtherUserId = c.otherUser.Id,
                OtherUserName = c.otherUser.UserName,
                OtherUserAvatarUrl = c.otherUser.AvatarUrl,
                LastMessageContent = c.lastMessage.Content,
                LastMessageAt = c.lastMessage.CreatedAt,
                UnreadCount = c.unreadCount
            }).ToList();
        }

        public async Task MarkAsReadAsync(string userId, IEnumerable<string> messageIds)
        {
            await _uow.Messages.MarkAsReadAsync(userId, messageIds);
        }

        public async Task MarkConversationAsReadAsync(string userId, string otherUserId)
        {
            await _uow.Messages.MarkConversationAsReadAsync(userId, otherUserId);
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _uow.Messages.GetUnreadCountAsync(userId);
        }
    }
}

