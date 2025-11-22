using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class TutorProfileRepository : GenericRepository<TutorProfile>, ITutorProfileRepository
    {
        public TutorProfileRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<TutorProfile?> GetByUserIdAsync(string userId)
            => await _dbSet.Include(t => t.User)
                       .FirstOrDefaultAsync(t => t.UserId == userId);

        public async Task<IReadOnlyList<(User user, TutorProfile profile)>> GetPendingTutorsAsync()
        {
            var query = from p in _context.TutorProfiles
                        join u in _context.Users on p.UserId equals u.Id
                        where u.RoleName == "Tutor" && u.Status == AccountStatus.PendingApproval
                        select new { user = u, profile = p };

            var rows = await query.ToListAsync();
            return rows.Select(x => (x.user, x.profile)).ToList();
        }

        public async Task<PaginationResult<TutorProfile>> GetApprovedPagedAsync(int pageNumber, int pageSize)
        {
            IQueryable<TutorProfile> q = _dbSet
                .Include(t => t.User)
                .Where(t => t.ReviewStatus == ReviewStatus.Approved
                            && t.User != null
                            && t.User.Status == AccountStatus.Active
                            && !t.User.IsBanned)
                .OrderByDescending(t => t.User!.CreatedAt);

            var total = await q.CountAsync();
            var items = await q.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return new PaginationResult<TutorProfile>(items, total, pageNumber, pageSize);
        }

        public async Task<PaginationResult<TutorProfile>> SearchAndFilterApprovedAsync(
            string? keyword,
            string? subject,
            string? educationLevel,
            ClassMode? mode,
            string? area,
            Gender? gender,
            double? minRating,
            decimal? minPrice,
            decimal? maxPrice,
            int pageNumber,
            int pageSize)
        {
            IQueryable<TutorProfile> q = _dbSet
                .Include(t => t.User)
                .Include(t => t.Classes)
                .Where(t => t.ReviewStatus == ReviewStatus.Approved
                            && t.User != null
                            && t.User.Status == AccountStatus.Active
                            && !t.User.IsBanned);

            // Search by keyword (tên gia sư, môn học, mô tả)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordLower = keyword.ToLower();
                q = q.Where(t =>
                    (t.User!.UserName != null && t.User.UserName.ToLower().Contains(keywordLower)) ||
                    (t.User.Email != null && t.User.Email.ToLower().Contains(keywordLower)) ||
                    (t.TeachingSubjects != null && t.TeachingSubjects.ToLower().Contains(keywordLower)) ||
                    (t.Bio != null && t.Bio.ToLower().Contains(keywordLower)) ||
                    (t.ExperienceDetails != null && t.ExperienceDetails.ToLower().Contains(keywordLower)) ||
                    (t.University != null && t.University.ToLower().Contains(keywordLower)) ||
                    (t.Major != null && t.Major.ToLower().Contains(keywordLower)));
            }

            // Filter by subject
            if (!string.IsNullOrWhiteSpace(subject))
            {
                q = q.Where(t => t.TeachingSubjects != null && t.TeachingSubjects.Contains(subject));
            }

            // Filter by education level
            if (!string.IsNullOrWhiteSpace(educationLevel))
            {
                q = q.Where(t => t.EducationLevel != null && t.EducationLevel.Contains(educationLevel));
            }

            // Filter by mode (online/offline) - check through Classes
            if (mode.HasValue)
            {
                q = q.Where(t => t.Classes.Any(c => c.Mode == mode.Value && c.Status == ClassStatus.Active));
            }

            // Filter by area (from User.Address)
            if (!string.IsNullOrWhiteSpace(area))
            {
                var areaLower = area.ToLower();
                q = q.Where(t => t.User!.Address != null && t.User.Address.ToLower().Contains(areaLower));
            }

            // Filter by gender
            if (gender.HasValue)
            {
                q = q.Where(t => t.User!.Gender == gender.Value);
            }

            // Filter by min rating
            if (minRating.HasValue)
            {
                q = q.Where(t => t.Rating != null && t.Rating >= minRating.Value);
            }

            // Filter by price range (from Classes)
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                q = q.Where(t => t.Classes.Any(c =>
                    c.Status == ClassStatus.Active &&
                    (!minPrice.HasValue || c.Price >= minPrice.Value) &&
                    (!maxPrice.HasValue || c.Price <= maxPrice.Value)));
            }

            // Order by rating (desc) then by created date
            q = q.OrderByDescending(t => t.Rating ?? 0)
                 .ThenByDescending(t => t.User!.CreatedAt);

            var total = await q.CountAsync();
            var items = await q.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

            return new PaginationResult<TutorProfile>(items, total, pageNumber, pageSize);
        }

        public async Task<TutorProfile?> GetApprovedByUserIdAsync(string userId)
            => await _dbSet.Include(t => t.User)
                           .FirstOrDefaultAsync(t => t.UserId == userId
                                                   && t.ReviewStatus == ReviewStatus.Approved
                                                   && t.User != null
                                                   && t.User.Status == AccountStatus.Active
                                                   && !t.User.IsBanned);

        public async Task<string?> GetTutorUserIdByTutorProfileIdAsync(string tutorProfileId)
        {
            var tp = await _dbSet.AsNoTracking()
                                 .Where(t => t.Id == tutorProfileId)
                                 .Select(t => t.UserId)
                                 .FirstOrDefaultAsync();
            return tp;
        }

        public async Task<string?> GetIdByUserIdAsync(string userId)
        => await _dbSet.AsNoTracking()
                       .Where(t => t.UserId == userId)
                       .Select(t => t.Id)
                       .FirstOrDefaultAsync();
    }
}
