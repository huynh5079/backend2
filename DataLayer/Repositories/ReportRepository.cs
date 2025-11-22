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
    public class ReportRepository : GenericRepository<Report>, IReportRepository
    {
        public ReportRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<(IReadOnlyList<Report> items, int total)> GetPagedForTutorAsync(string tutorUserId, ReportStatus? status, string? keyword, int page, int pageSize)
        {
            var q = _dbSet.AsNoTracking().Where(r => r.TargetUserId == tutorUserId);

            if (status.HasValue) q = q.Where(r => r.Status == status);
            if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(r => r.Description!.Contains(keyword));

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }

        public async Task<(IReadOnlyList<Report> items, int total)> GetPagedForAdminAsync(ReportStatus? status, string? keyword, int page, int pageSize)
        {
            var q = _dbSet.AsNoTracking().Where(r => r.TargetUserId == null); // null => gửi Admin

            if (status.HasValue) q = q.Where(r => r.Status == status);
            if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(r => r.Description!.Contains(keyword));

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
            return (items, total);
        }

        public async Task<Report?> GetDetailAsync(string id)
        {
            return await _dbSet
                .Include(r => r.Reporter)
                .Include(r => r.TargetUser)
                .Include(r => r.TargetLesson)
                .Include(r => r.TargetMedia)
                .FirstOrDefaultAsync(r => r.Id == id);
        }
    }

}
