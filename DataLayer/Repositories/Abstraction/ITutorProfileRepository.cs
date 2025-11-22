using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface ITutorProfileRepository : IGenericRepository<TutorProfile>
    {
        Task<TutorProfile?> GetByUserIdAsync(string userId);
        Task<IReadOnlyList<(User user, TutorProfile profile)>> GetPendingTutorsAsync();

        // Public
        Task<PaginationResult<TutorProfile>> GetApprovedPagedAsync(int pageNumber, int pageSize);
        Task<PaginationResult<TutorProfile>> SearchAndFilterApprovedAsync(
            string? keyword,
            string? subject,
            string? educationLevel,
            DataLayer.Enum.ClassMode? mode,
            string? area,
            DataLayer.Enum.Gender? gender,
            double? minRating,
            decimal? minPrice,
            decimal? maxPrice,
            int pageNumber,
            int pageSize);
        Task<TutorProfile?> GetApprovedByUserIdAsync(string userId);
        Task<string?> GetTutorUserIdByTutorProfileIdAsync(string tutorProfileId);
        Task<string?> GetIdByUserIdAsync(string userId);
    }
}
