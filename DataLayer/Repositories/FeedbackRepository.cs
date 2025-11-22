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
    public class FeedbackRepository : GenericRepository<Feedback>, IFeedbackRepository
    {
        public FeedbackRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<bool> ExistsAsync(string fromUserId, string toUserId, string classId)
        {
            return await _dbSet.AnyAsync(f =>
                f.FromUserId == fromUserId &&
                f.ToUserId == toUserId &&
                f.ClassId == classId);
        }

        public async Task<List<Feedback>> GetByClassAsync(string classId)
        {
            return await _dbSet
                .Include(f => f.FromUser)
                .Include(f => f.ToUser)
                .Where(f => f.ClassId == classId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        // Chỉ lấy feedback "public" trên trang Tutor (LessonId == null & IsPublicOnTutorProfile == true)
        public async Task<(List<Feedback> items, int total)> GetByTutorUserAsync(string tutorUserId, int page, int pageSize)
        {
            var q = _dbSet
                .Include(f => f.FromUser)
                .Include(f => f.ToUser)
                .Where(f => f.ToUserId == tutorUserId &&
                            f.LessonId == null &&
                            f.IsPublicOnTutorProfile == true)
                .OrderByDescending(f => f.CreatedAt);

            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        // Tính rating: trung bình theo từng người học (avg per rater) -> rồi trung bình các raters
        // Chỉ tính với feedback CÓ ClassId (tức feedback class), Rating != null và Class COMPLETED
        public async Task<(double avg, int count)> CalcTutorRatingAsync(string tutorUserId)
        {
            var q =
                from f in _context.Feedbacks
                join c in _context.Classes on f.ClassId equals c.Id
                where f.ToUserId == tutorUserId
                      && f.ClassId != null
                      && f.Rating != null
                      && (c.Status == ClassStatus.Ongoing || c.Status == ClassStatus.Completed)
                group f by f.FromUserId into g
                select new
                {
                    FromUserId = g.Key!,
                    UserAvg = g.Average(x => (double)x.Rating!)
                };

            var perUser = await q.ToListAsync();
            if (perUser.Count == 0) return (0, 0);

            var overall = perUser.Average(x => x.UserAvg);
            return (overall, perUser.Count);
        }
    }
}
