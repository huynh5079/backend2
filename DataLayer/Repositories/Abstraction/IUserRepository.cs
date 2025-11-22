using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> FindByEmailAsync(string email);
        Task<bool> ExistsByEmailAsync(string email);
        Task<User?> FindWithRoleByIdAsync(string userId);
        Task<PaginationResult<User>> GetPagedByRoleAsync(string roleName, int pageNumber, int pageSize);
        Task<User?> GetDetailByIdAsync(string userId);
    }
}
