using BusinessLayer.DTOs.Chat;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IChatHubService
    {
        Task SendMessageToUserAsync(string userId, MessageDto message);
    }
}

