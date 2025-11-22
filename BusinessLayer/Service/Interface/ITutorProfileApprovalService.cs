using BusinessLayer.DTOs.Admin.TutorProfileApproval;
using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BusinessLayer.DTOs.Admin.Directory.AdminListDtos;

namespace BusinessLayer.Service.Interface
{
    public interface ITutorProfileApprovalService
    {
        Task<IReadOnlyList<TutorReviewItemDto>> GetPendingAsync();
        Task<TutorReviewDetailDto?> GetDetailAsync(string userId);
        Task<(bool ok, string message)> ApproveAsync(string userId);
        Task<(bool ok, string message)> RejectAsync(string userId, string rejectReason);
        Task<(bool ok, string message)> ProvideAsync(string userId, string? note);
        Task<PaginationResult<TutorListItemDto>> GetTutorsPagedAsync(int page, int pageSize);
    }
}
