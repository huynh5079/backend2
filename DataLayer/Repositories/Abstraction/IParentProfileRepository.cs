using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IParentProfileRepository : IGenericRepository<ParentProfile>
    {
        Task<ParentProfile?> GetByUserIdAsync(string userId);
        Task<(ParentProfile? parent, StudentProfile? linkedStudent, User? linkedStudentUser)> GetWithLinkedStudentAsync(string userId);
        Task<ParentProfile?> GetLinkAsync(string parentUserId, string studentId);
        Task<bool> ExistsLinkAsync(string parentUserId, string studentId);
        Task<PaginationResult<(ParentProfile link, StudentProfile stu, User childUser)>> GetChildrenPagedAsync(string parentUserId, int page, int pageSize);
        Task<IReadOnlyList<(ParentProfile link, StudentProfile stu, User childUser)>> GetChildrenAllAsync(string parentUserId);
        Task<List<string>> GetChildrenIdsAsync(string parentUserId);
    }
}
