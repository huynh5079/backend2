using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.SignalR;
using TPEdu_API.Hubs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TPEdu_API.Services
{
    public class ChatHubService : IChatHubService
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ChatConnectionManager _connectionManager;

        public ChatHubService(IHubContext<ChatHub> hubContext, ChatConnectionManager connectionManager)
        {
            _hubContext = hubContext;
            _connectionManager = connectionManager;
        }

        public async Task SendMessageToUserAsync(string userId, MessageDto message)
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveMessage", message);
        }

        /// <summary>
        /// Kiểm tra user có online không
        /// </summary>
        public bool IsUserOnline(string userId)
        {
            return _connectionManager.IsUserOnline(userId);
        }

        /// <summary>
        /// Lấy danh sách users online
        /// </summary>
        public List<string> GetOnlineUsers()
        {
            return _connectionManager.GetOnlineUsers();
        }

        /// <summary>
        /// Lấy danh sách users online từ một list userIds
        /// </summary>
        public List<string> GetOnlineUsersFromList(List<string> userIds)
        {
            return _connectionManager.GetOnlineUsersFromList(userIds);
        }
    }
}

