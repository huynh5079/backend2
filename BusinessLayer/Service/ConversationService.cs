using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class ConversationService : IConversationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IScheduleUnitOfWork _scheduleUow;

        public ConversationService(IUnitOfWork uow, IScheduleUnitOfWork scheduleUow)
        {
            _uow = uow;
            _scheduleUow = scheduleUow;
        }

        public async Task<ConversationDto> GetOrCreateOneToOneConversationAsync(string userId1, string userId2)
        {
            // Tìm conversation 1-1 đã có
            var existing = await _uow.Conversations.GetOneToOneConversationAsync(userId1, userId2);
            
            if (existing != null)
            {
                return MapToDto(existing, userId1);
            }

            // Tạo mới conversation 1-1
            var user1 = await _uow.Users.GetByIdAsync(userId1);
            var user2 = await _uow.Users.GetByIdAsync(userId2);
            
            if (user1 == null || user2 == null)
                throw new ArgumentException("User không tồn tại");

            var conversation = new Conversation
            {
                Title = $"{user1.UserName} & {user2.UserName}",
                Type = ConversationType.OneToOne,
                LastMessageAt = DateTime.Now
            };

            await _uow.Conversations.CreateAsync(conversation);
            await _uow.SaveChangesAsync();

            // Thêm participants
            await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId1,
                Role = "Member"
            });

            await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId2,
                Role = "Member"
            });

            return MapToDto(conversation, userId1);
        }

        public async Task<ConversationDto> GetOrCreateClassConversationAsync(string classId, string userId)
        {
            // Kiểm tra quyền: user phải là tutor hoặc student của lớp
            var cls = await _uow.Classes.GetByIdAsync(classId);
            if (cls == null)
                throw new ArgumentException("Lớp học không tồn tại");

            // Tìm conversation đã có
            var existing = await _uow.Conversations.GetClassConversationAsync(classId);
            if (existing != null)
            {
                // Kiểm tra user có trong conversation chưa
                var participant = await _uow.Conversations.GetParticipantAsync(existing.Id, userId);
                if (participant == null)
                {
                    // Thêm user vào conversation
                    await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
                    {
                        ConversationId = existing.Id,
                        UserId = userId,
                        Role = "Member"
                    });
                }
                return MapToDto(existing, userId);
            }

            // Tạo mới conversation cho lớp
            var conversation = new Conversation
            {
                Title = cls.Title ?? $"Lớp {classId}",
                Type = ConversationType.Class,
                ClassId = classId,
                LastMessageAt = DateTime.Now
            };

            await _uow.Conversations.CreateAsync(conversation);
            await _uow.SaveChangesAsync();

            // Thêm tutor
            var tutorUserId = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
            if (!string.IsNullOrEmpty(tutorUserId))
            {
                await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = tutorUserId,
                    Role = "Admin"
                });
            }

            // Thêm user hiện tại
            await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId,
                Role = "Member"
            });

            return MapToDto(conversation, userId);
        }

        public async Task<ConversationDto> GetOrCreateClassRequestConversationAsync(string classRequestId, string userId)
        {
            var request = await _scheduleUow.ClassRequests.GetByIdAsync(classRequestId);
            if (request == null)
                throw new ArgumentException("ClassRequest không tồn tại");

            var existing = await _uow.Conversations.GetClassRequestConversationAsync(classRequestId);
            if (existing != null)
            {
                var participant = await _uow.Conversations.GetParticipantAsync(existing.Id, userId);
                if (participant == null)
                {
                    await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
                    {
                        ConversationId = existing.Id,
                        UserId = userId,
                        Role = "Member"
                    });
                }
                return MapToDto(existing, userId);
            }

            var conversation = new Conversation
            {
                Title = $"Yêu cầu lớp học {classRequestId}",
                Type = ConversationType.ClassRequest,
                ClassRequestId = classRequestId,
                LastMessageAt = DateTime.Now
            };

            await _uow.Conversations.CreateAsync(conversation);
            await _uow.SaveChangesAsync();

            // Thêm student
            var studentProfile = await _uow.StudentProfiles.GetByIdAsync(request.StudentId);
            if (studentProfile != null && !string.IsNullOrEmpty(studentProfile.UserId))
            {
                await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = studentProfile.UserId,
                    Role = "Admin"
                });
            }

            // Thêm user hiện tại
            await _uow.Conversations.AddParticipantAsync(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId,
                Role = "Member"
            });

            return MapToDto(conversation, userId);
        }

        public async Task<IReadOnlyList<ConversationDto>> GetUserConversationsAsync(string userId)
        {
            var conversations = await _uow.Conversations.GetUserConversationsAsync(userId);
            return conversations.Select(c => MapToDto(c, userId)).ToList();
        }

        public async Task<ConversationDto?> GetConversationByIdAsync(string conversationId, string userId)
        {
            var conversation = await _uow.Conversations.GetByIdWithParticipantsAsync(conversationId);
            if (conversation == null)
                return null;

            // Kiểm tra user có trong conversation không
            var participant = await _uow.Conversations.GetParticipantAsync(conversationId, userId);
            if (participant == null)
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập conversation này");

            return MapToDto(conversation, userId);
        }

        private ConversationDto MapToDto(Conversation c, string currentUserId)
        {
            var otherParticipant = c.Participants
                .Where(p => p.UserId != currentUserId)
                .Select(p => p.User)
                .FirstOrDefault();

            // Lấy tin nhắn cuối (cần load Messages nếu chưa có)
            Message? lastMessage = null;
            if (c.Messages != null && c.Messages.Any())
            {
                lastMessage = c.Messages
                    .Where(m => m.DeletedAt == null)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();
            }

            var currentParticipant = c.Participants.FirstOrDefault(p => p.UserId == currentUserId);

            return new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                Type = c.Type,
                OtherUserId = otherParticipant?.Id,
                OtherUserName = otherParticipant?.UserName,
                OtherUserAvatarUrl = otherParticipant?.AvatarUrl,
                ClassId = c.ClassId,
                ClassTitle = c.Class?.Title,
                ClassRequestId = c.ClassRequestId,
                LastMessageContent = lastMessage?.Content,
                LastMessageType = lastMessage?.MessageType,
                LastMessageAt = lastMessage?.CreatedAt ?? c.LastMessageAt,
                UnreadCount = currentParticipant?.UnreadCount ?? 0,
                Participants = c.Participants.Select(p => new ParticipantDto
                {
                    UserId = p.UserId,
                    UserName = p.User?.UserName,
                    AvatarUrl = p.User?.AvatarUrl,
                    Role = p.Role
                }).ToList()
            };
        }
    }
}

