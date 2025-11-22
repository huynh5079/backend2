using BusinessLayer.DTOs.Chat;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.SignalR;
using TPEdu_API.Hubs;
using System.Threading.Tasks;

namespace TPEdu_API.Services
{
    public class ChatHubService : IChatHubService
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatHubService(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendMessageToUserAsync(string userId, MessageDto message)
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveMessage", message);
        }
    }
}

