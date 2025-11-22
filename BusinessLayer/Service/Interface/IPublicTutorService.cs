using BusinessLayer.DTOs.Admin.Tutors;
using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IPublicTutorService
    {
        Task<PaginationResult<PublicTutorListItemDto>> GetApprovedTutorsPagedAsync(int page, int pageSize = 6);
        Task<PaginationResult<PublicTutorListItemDto>> SearchAndFilterTutorsAsync(TutorSearchFilterDto filter);
        Task<PublicTutorDetailDto?> GetApprovedTutorDetailAsync(string userId);
    }
}
