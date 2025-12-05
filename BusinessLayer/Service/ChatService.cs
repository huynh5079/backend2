using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using BusinessLayer.Storage;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _uow;
        private readonly IChatHubService _hubService;
        private readonly IFileStorageService _storage;
        private readonly IScheduleUnitOfWork _scheduleUow;

        public ChatService(IUnitOfWork uow, IChatHubService hubService, IFileStorageService storage, IScheduleUnitOfWork scheduleUow)
        {
            _uow = uow;
            _hubService = hubService;
            _storage = storage;
            _scheduleUow = scheduleUow;
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

        public async Task<MessageDto> SendMessageWithFileAsync(string senderId, SendMessageDto dto, IFormFile? file, CancellationToken ct = default)
        {
            MessageType messageType = MessageType.Text;
            string? fileUrl = null;
            string? fileName = null;
            string? mediaType = null;
            long? fileSize = null;

            // Upload file nếu có
            if (file != null && file.Length > 0)
            {
                var uploadResults = await _storage.UploadManyAsync(
                    new[] { file },
                    UploadContext.Chat,
                    senderId,
                    ct);

                var uploadResult = uploadResults.FirstOrDefault();
                if (uploadResult != null)
                {
                    fileUrl = uploadResult.Url;
                    fileName = uploadResult.FileName;
                    mediaType = uploadResult.ContentType;
                    fileSize = uploadResult.FileSize;
                    messageType = uploadResult.Kind == FileKind.Image ? MessageType.Image : MessageType.File;
                }
            }

            // Tìm hoặc tạo conversation
            string? conversationId = dto.ConversationId;
            if (string.IsNullOrEmpty(conversationId) && !string.IsNullOrEmpty(dto.ReceiverId))
            {
                var conversationService = new ConversationService(_uow, _scheduleUow);
                var conversation = await conversationService.GetOrCreateOneToOneConversationAsync(senderId, dto.ReceiverId);
                conversationId = conversation.Id;
            }

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = dto.ReceiverId,
                ConversationId = conversationId,
                Content = dto.Content,
                MessageType = messageType,
                FileUrl = fileUrl,
                FileName = fileName,
                MediaType = mediaType,
                FileSize = fileSize,
                Status = "Sent",
                CreatedAt = DateTime.Now
            };

            await _uow.Messages.CreateAsync(message);
            await _uow.SaveChangesAsync();

            // Cập nhật LastMessageAt
            if (!string.IsNullOrEmpty(conversationId))
            {
                var conversation = await _uow.Conversations.GetByIdAsync(conversationId);
                if (conversation != null)
                {
                    conversation.LastMessageAt = DateTime.Now;
                    await _uow.Conversations.UpdateAsync(conversation);
                    await _uow.SaveChangesAsync();
                }
            }

            var sender = await _uow.Users.GetByIdAsync(senderId);
            var messageDto = new MessageDto
            {
                Id = message.Id,
                SenderId = senderId,
                SenderName = sender?.UserName,
                SenderAvatarUrl = sender?.AvatarUrl,
                ReceiverId = dto.ReceiverId,
                ConversationId = conversationId,
                Content = message.Content,
                MessageType = messageType,
                FileUrl = fileUrl,
                FileName = fileName,
                MediaType = mediaType,
                FileSize = fileSize,
                Status = message.Status,
                CreatedAt = message.CreatedAt
            };

            // Push realtime
            if (!string.IsNullOrEmpty(dto.ReceiverId))
            {
                await _hubService.SendMessageToUserAsync(dto.ReceiverId, messageDto);
            }
            else if (!string.IsNullOrEmpty(conversationId))
            {
                var conversation = await _uow.Conversations.GetByIdWithParticipantsAsync(conversationId);
                if (conversation != null)
                {
                    foreach (var participant in conversation.Participants.Where(p => p.UserId != senderId))
                    {
                        await _hubService.SendMessageToUserAsync(participant.UserId, messageDto);
                    }
                }
            }

            return messageDto;
        }

        public async Task<PaginationResult<MessageDto>> GetMessagesByConversationIdAsync(string conversationId, string userId, int page, int pageSize)
        {
            // Kiểm tra quyền
            var participant = await _uow.Conversations.GetParticipantAsync(conversationId, userId);
            if (participant == null)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập conversation này");

            var rs = await _uow.Messages.GetMessagesByConversationIdAsync(conversationId, page, pageSize);

            var messageDtos = rs.Data.Select(m => new MessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId!,
                SenderName = m.Sender?.UserName,
                SenderAvatarUrl = m.Sender?.AvatarUrl,
                ReceiverId = m.ReceiverId,
                ConversationId = m.ConversationId,
                Content = m.Content,
                MessageType = m.MessageType,
                FileUrl = m.FileUrl,
                FileName = m.FileName,
                MediaType = m.MediaType,
                FileSize = m.FileSize,
                Status = m.Status,
                IsEdited = m.IsEdited,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            }).ToList();

            return new PaginationResult<MessageDto>(messageDtos, rs.TotalCount, rs.PageNumber, rs.PageSize);
        }

        public async Task<MessageDto> EditMessageAsync(string messageId, string userId, string newContent)
        {
            var message = await _uow.Messages.GetByIdAsync(messageId);
            if (message == null || message.SenderId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền sửa tin nhắn này");

            if (message.DeletedAt != null)
                throw new InvalidOperationException("Tin nhắn đã bị xóa");

            message.Content = newContent;
            message.IsEdited = true;
            message.UpdatedAt = DateTime.Now;

            await _uow.Messages.UpdateAsync(message);
            await _uow.SaveChangesAsync();

            var sender = await _uow.Users.GetByIdAsync(userId);
            var messageDto = new MessageDto
            {
                Id = message.Id,
                SenderId = userId,
                SenderName = sender?.UserName,
                SenderAvatarUrl = sender?.AvatarUrl,
                ReceiverId = message.ReceiverId,
                ConversationId = message.ConversationId,
                Content = message.Content,
                MessageType = message.MessageType,
                FileUrl = message.FileUrl,
                FileName = message.FileName,
                MediaType = message.MediaType,
                FileSize = message.FileSize,
                Status = message.Status,
                IsEdited = true,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt
            };

            // Gửi realtime notification đến tất cả participants
            if (!string.IsNullOrEmpty(message.ConversationId))
            {
                var conversation = await _uow.Conversations.GetByIdWithParticipantsAsync(message.ConversationId);
                if (conversation != null)
                {
                    foreach (var participant in conversation.Participants)
                    {
                        await _hubService.SendMessageToUserAsync(participant.UserId, messageDto);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(message.ReceiverId))
            {
                // Chat 1-1 cũ
                await _hubService.SendMessageToUserAsync(message.ReceiverId, messageDto);
            }

            return messageDto;
        }

        public async Task<bool> DeleteMessageAsync(string messageId, string userId, bool deleteForEveryone)
        {
            var message = await _uow.Messages.GetByIdAsync(messageId);
            if (message == null || message.SenderId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền xóa tin nhắn này");

            message.DeletedAt = DateTime.Now;
            if (deleteForEveryone)
            {
                message.Status = "Deleted";
            }

            await _uow.Messages.UpdateAsync(message);
            await _uow.SaveChangesAsync();

            // Gửi realtime notification đến tất cả participants
            if (deleteForEveryone)
            {
                var sender = await _uow.Users.GetByIdAsync(userId);
                var messageDto = new MessageDto
                {
                    Id = message.Id,
                    SenderId = userId,
                    SenderName = sender?.UserName,
                    SenderAvatarUrl = sender?.AvatarUrl,
                    ReceiverId = message.ReceiverId,
                    ConversationId = message.ConversationId,
                    Content = "[Tin nhắn đã bị xóa]",
                    MessageType = message.MessageType,
                    Status = "Deleted",
                    CreatedAt = message.CreatedAt,
                    UpdatedAt = message.UpdatedAt
                };

                if (!string.IsNullOrEmpty(message.ConversationId))
                {
                    var conversation = await _uow.Conversations.GetByIdWithParticipantsAsync(message.ConversationId);
                    if (conversation != null)
                    {
                        foreach (var participant in conversation.Participants)
                        {
                            await _hubService.SendMessageToUserAsync(participant.UserId, messageDto);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(message.ReceiverId))
                {
                    // Chat 1-1 cũ
                    await _hubService.SendMessageToUserAsync(message.ReceiverId, messageDto);
                }
            }

            return true;
        }
    }
}

