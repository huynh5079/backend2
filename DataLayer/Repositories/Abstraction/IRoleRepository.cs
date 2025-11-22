using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IRoleRepository : IGenericRepository<Role>
    {
        Task<Role?> GetByEnumAsync(RoleEnum roleEnum);
    }
}
