using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IFavoriteTutorRepository : IGenericRepository<FavoriteTutor>
    {
        Task<bool> IsFavoritedAsync(string userId, string tutorProfileId);
        Task<List<FavoriteTutor>> GetByUserIdAsync(string userId);
        Task<int> CountByTutorProfileIdAsync(string tutorProfileId);
        Task<FavoriteTutor?> GetAsync(string userId, string tutorProfileId);
        Task<FavoriteTutor?> GetIncludingDeletedAsync(string userId, string tutorProfileId);
    }
}
