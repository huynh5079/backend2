using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BusinessLayer.DTOs.Admin.Directory.AdminListDtos;

namespace BusinessLayer.Service.Interface
{
    public interface IAdminDirectoryService
    {
        Task<PaginationResult<TutorListItemDto>> GetTutorsPagedAsync(int page, int pageSize);
        Task<AdminStudentListPageDto> GetStudentsPagedAsync(int page, int pageSize);
        Task<AdminParentListPageDto> GetParentsPagedAsync(int page, int pageSize);

        Task<AdminStudentDetailDto?> GetStudentDetailAsync(string userId);
        Task<AdminParentDetailDto?> GetParentDetailAsync(string userId);
    }
}
