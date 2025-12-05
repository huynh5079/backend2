using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class FavoriteTutorRepository : GenericRepository<FavoriteTutor>, IFavoriteTutorRepository
    {
        public FavoriteTutorRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<bool> IsFavoritedAsync(string userId, string tutorProfileId)
        {
            return await _dbSet.AnyAsync(f => 
                f.UserId == userId && 
                f.TutorProfileId == tutorProfileId &&
                f.DeletedAt == null);
        }

        public async Task<List<FavoriteTutor>> GetByUserIdAsync(string userId)
        {
            return await _dbSet
                .Include(f => f.TutorProfile)
                    .ThenInclude(tp => tp.User)
                .Where(f => f.UserId == userId && f.DeletedAt == null)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> CountByTutorProfileIdAsync(string tutorProfileId)
        {
            return await _dbSet.CountAsync(f => 
                f.TutorProfileId == tutorProfileId && 
                f.DeletedAt == null);
        }

        public async Task<FavoriteTutor?> GetAsync(string userId, string tutorProfileId)
        {
            return await _dbSet.FirstOrDefaultAsync(f =>
                f.UserId == userId &&
                f.TutorProfileId == tutorProfileId &&
                f.DeletedAt == null);
        }

        public async Task<FavoriteTutor?> GetIncludingDeletedAsync(string userId, string tutorProfileId)
        {
            return await _dbSet.FirstOrDefaultAsync(f =>
                f.UserId == userId &&
                f.TutorProfileId == tutorProfileId);
        }
    }
}
