using DataLayer.Entities;
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
    public class ParentProfileRepository : GenericRepository<ParentProfile>, IParentProfileRepository
    {
        public ParentProfileRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<ParentProfile?> GetByUserIdAsync(string userId)
            => await _dbSet.FirstOrDefaultAsync(p => p.UserId == userId);

        public async Task<(ParentProfile? parent, StudentProfile? linkedStudent, User? linkedStudentUser)> GetWithLinkedStudentAsync(string userId)
        {
            var parent = await _dbSet.FirstOrDefaultAsync(p => p.UserId == userId);
            if (parent == null || string.IsNullOrEmpty(parent.LinkedStudentId))
                return (parent, null, null);

            var stu = await _context.StudentProfiles
                                    .Include(s => s.User)
                                    .FirstOrDefaultAsync(s => s.Id == parent.LinkedStudentId);
            return (parent, stu, stu?.User);
        }

        public async Task<ParentProfile?> GetLinkAsync(string parentUserId, string studentId)
            => await _dbSet.FirstOrDefaultAsync(x => x.UserId == parentUserId && x.LinkedStudentId == studentId);

        public async Task<bool> ExistsLinkAsync(string parentUserId, string studentId)
            => await _dbSet.AnyAsync(x => x.UserId == parentUserId && x.LinkedStudentId == studentId);

        public async Task<PaginationResult<(ParentProfile link, StudentProfile stu, User childUser)>> GetChildrenPagedAsync(string parentUserId, int page, int pageSize)
        {
            var q = _dbSet.Where(p => p.UserId == parentUserId)
                          .Join(_context.StudentProfiles.Include(s => s.User),
                                p => p.LinkedStudentId,
                                s => s.Id,
                                (p, s) => new { p, s, u = s.User! })
                          .OrderByDescending(x => x.s.CreatedAt);

            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

            var data = items.Select(x => (x.p, x.s, x.u)).ToList();
            return new PaginationResult<(ParentProfile, StudentProfile, User)>(data, total, page, pageSize);
        }

        public async Task<IReadOnlyList<(ParentProfile link, StudentProfile stu, User childUser)>> GetChildrenAllAsync(string parentUserId)
        {
            var q = _dbSet.Where(p => p.UserId == parentUserId)
                          .Join(_context.StudentProfiles.Include(s => s.User),
                                p => p.LinkedStudentId,
                                s => s.Id,
                                (p, s) => new { p, s, u = s.User! })
                          .OrderByDescending(x => x.s.CreatedAt);

            var rows = await q.ToListAsync();
            return rows.Select(x => (x.p, x.s, x.u)).ToList();
        }

        public async Task<List<string>> GetChildrenIdsAsync(string parentUserId)
        {
            return await _context.ParentProfiles
                .Where(p => p.UserId == parentUserId && p.LinkedStudentId != null)
                .Select(p => p.LinkedStudentId!)
                .ToListAsync();
        }
    }
}
