using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IStudentProfileRepository : IGenericRepository<StudentProfile>
    {
        Task<StudentProfile?> GetByUserIdAsync(string userId);
        Task<string?> GetIdByUserIdAsync(string userId);
    }
}
