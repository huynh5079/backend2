using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Schedule
{
    public class ClassAssignRepository : GenericRepository<ClassAssign>, IClassAssignRepository
    {
        public ClassAssignRepository(TpeduContext context) : base(context)
        {
            // Chỉ cần gọi constructor của base
        }
        // Che hàm CreateAsync

        public new async Task CreateAsync(ClassAssign entity)
        {
            await _dbSet.AddAsync(entity);
            // KHÔNG GỌI SaveChangesAsync()
        }

        // Che hàm UpdateAsync của base
        public new Task UpdateAsync(ClassAssign entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
            // KHÔNG GỌI SaveChangesAsync()
        }

        public async Task<bool> IsApprovedAsync(string classId, string studentProfileId)
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(ca => ca.ClassId == classId
                             && ca.StudentId == studentProfileId
                             && ca.ApprovalStatus == ApprovalStatus.Approved);
        }

        public async Task<bool> IsAnyChildApprovedAsync(string classId, List<string> studentProfileIds)
        {
            // _dbSet ở đây là IQueryable<ClassAssign> nên có thể dùng AnyAsync
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(ca => ca.ClassId == classId
                             && ca.StudentId != null
                             && studentProfileIds.Contains(ca.StudentId) // Kiểm tra xem có bất kỳ ID con nào trong danh sách
                             && ca.ApprovalStatus == ApprovalStatus.Approved);
        }

        public async Task<List<User>> GetParticipantsInClassAsync(string classId)
        {
            // Lấy tất cả Student User
            var studentUsers = await _dbSet
                .Where(ca => ca.ClassId == classId &&
                             ca.ApprovalStatus == ApprovalStatus.Approved &&
                             ca.Student != null &&
                             ca.Student.User != null)
                .Select(ca => ca.Student!.User!)
                .ToListAsync();

            // Lấy tất cả Parent User
            var parentUsers = await _dbSet
                .Where(ca => ca.ClassId == classId &&
                             ca.ApprovalStatus == ApprovalStatus.Approved &&
                             ca.Student != null)
                .SelectMany(ca => ca.Student!.ParentProfiles.Select(pp => pp.User)) // Join qua Student -> ParentProfile -> User
                .Where(user => user != null)
                .ToListAsync();

            // Gộp 2 danh sách và loại bỏ trùng lặp
            return studentUsers.Concat(parentUsers).DistinctBy(u => u.Id).ToList();
        }

        public async Task<List<ClassAssign>> GetByStudentIdAsync(string studentProfileId, bool includeClass = false)
        {
            IQueryable<ClassAssign> query = _dbSet
                .AsNoTracking()
                .Where(ca => ca.StudentId == studentProfileId);

            if (includeClass)
            {
                query = query.Include(ca => ca.Class)
                             .ThenInclude(c => c!.Tutor)
                             .ThenInclude(t => t!.User);
            }

            return await query.OrderByDescending(ca => ca.EnrolledAt ?? ca.CreatedAt)
                             .ToListAsync();
        }

        public async Task<ClassAssign?> GetByClassAndStudentAsync(string classId, string studentProfileId, bool includeClass = false)
        {
            IQueryable<ClassAssign> query = _dbSet
                .AsNoTracking()
                .Where(ca => ca.ClassId == classId && ca.StudentId == studentProfileId);

            if (includeClass)
            {
                query = query.Include(ca => ca.Class)
                             .ThenInclude(c => c!.Tutor)
                             .ThenInclude(t => t!.User);
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<ClassAssign>> GetByClassIdAsync(string classId, bool includeStudent = false)
        {
            IQueryable<ClassAssign> query = _dbSet
                .AsNoTracking()
                .Where(ca => ca.ClassId == classId);

            if (includeStudent)
            {
                query = query.Include(ca => ca.Student)
                             .ThenInclude(s => s!.User);
            }

            return await query.OrderByDescending(ca => ca.EnrolledAt ?? ca.CreatedAt)
                             .ToListAsync();
        }
    }
}
