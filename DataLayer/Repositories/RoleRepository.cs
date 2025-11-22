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
    public class RoleRepository : GenericRepository<Role>, IRoleRepository
    {
        public RoleRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<Role?> GetByEnumAsync(RoleEnum roleEnum)
        {
            // Role.RoleName là enum, đã cấu hình HasConversion<string>() trong OnModelCreating
            return await _dbSet.FirstOrDefaultAsync(r => r.RoleName == roleEnum);
        }
    }
}
