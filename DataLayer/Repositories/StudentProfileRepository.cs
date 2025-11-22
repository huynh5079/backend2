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
    public class StudentProfileRepository : GenericRepository<StudentProfile>, IStudentProfileRepository
    {
        public StudentProfileRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<StudentProfile?> GetByUserIdAsync(string userId)
            => await _dbSet.Include(s => s.User)
                           .FirstOrDefaultAsync(s => s.UserId == userId && s.DeletedAt == null);

        public async Task<string?> GetIdByUserIdAsync(string userId)
            => await _dbSet.AsNoTracking()
                           .Where(s => s.UserId == userId && s.DeletedAt == null)
                           .Select(s => s.Id)
                           .FirstOrDefaultAsync();
    }
}