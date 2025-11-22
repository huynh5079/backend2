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
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<User?> FindByEmailAsync(string email)
            => await _dbSet.Include(u => u.Role)
                           .FirstOrDefaultAsync(u => u.Email == email);

        public async Task<bool> ExistsByEmailAsync(string email)
            => await _dbSet.AnyAsync(u => u.Email == email);

        public async Task<User?> FindWithRoleByIdAsync(string userId)
            => await _dbSet.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);

        public async Task<PaginationResult<User>> GetPagedByRoleAsync(
            string roleName, int pageNumber, int pageSize)
        {
            IQueryable<User> query = _dbSet
                .Include(u => u.Role)
                .Where(u => u.RoleName == roleName)
                .OrderByDescending(u => u.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginationResult<User>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<User?> GetDetailByIdAsync(string userId)
            => await _dbSet.Include(u => u.Role)
                           .FirstOrDefaultAsync(u => u.Id == userId);
    }
}
