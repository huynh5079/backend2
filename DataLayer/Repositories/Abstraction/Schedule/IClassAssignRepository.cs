using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction.Schedule
{
    public interface IClassAssignRepository : IGenericRepository<ClassAssign>
    {
        Task<bool> IsApprovedAsync(string classId, string studentProfileId);
        Task<bool> IsAnyChildApprovedAsync(string classId, List<string> studentProfileIds);
        Task<List<User>> GetParticipantsInClassAsync(string classId);
        
        // New methods for enrollment management
        Task<List<ClassAssign>> GetByStudentIdAsync(string studentProfileId, bool includeClass = false);
        Task<ClassAssign?> GetByClassAndStudentAsync(string classId, string studentProfileId, bool includeClass = false);
        Task<List<ClassAssign>> GetByClassIdAsync(string classId, bool includeStudent = false);
    }
}
