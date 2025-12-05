using BusinessLayer.DTOs.FavoriteTutor;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IFavoriteTutorService
    {
        Task<FavoriteTutorDto> AddFavoriteAsync(string userId, string tutorProfileId);
        Task<bool> RemoveFavoriteAsync(string userId, string tutorProfileId);
        Task<List<FavoriteTutorDto>> GetMyFavoritesAsync(string userId);
        Task<bool> IsFavoritedAsync(string userId, string tutorProfileId);
    }
}
