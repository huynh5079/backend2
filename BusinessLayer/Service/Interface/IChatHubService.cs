using BusinessLayer.DTOs.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IChatHubService
    {
        Task SendMessageToUserAsync(string userId, MessageDto message);
        bool IsUserOnline(string userId);
        List<string> GetOnlineUsers();
        List<string> GetOnlineUsersFromList(List<string> userIds);
    }
}

