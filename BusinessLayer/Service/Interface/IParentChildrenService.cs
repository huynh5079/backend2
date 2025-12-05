using BusinessLayer.DTOs.Admin.Parent;
using BusinessLayer.DTOs.Profile;
using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IParentChildrenService
    {
        Task<PaginationResult<ChildListItemDto>> GetMyChildrenPagedAsync(string parentUserId, int page, int pageSize);
        Task<ChildDetailDto?> GetChildDetailAsync(string parentUserId, string studentId);

        Task<(bool ok, string message, ChildDetailDto? data)> CreateChildAsync(string parentUserId, CreateChildRequest req);
        Task<(bool ok, string message)> LinkExistingChildAsync(string parentUserId, LinkExistingChildRequest req);
        Task<(bool ok, string message, ChildDetailDto? data)> UpdateChildAsync(string parentUserId, string studentId, UpdateChildRequest req);
        Task<(bool ok, string message)> UnlinkChildAsync(string parentUserId, string studentId);

        // list all children ids of a parent
        Task<List<string>> GetChildrenIdsByParentUserIdAsync(string parentUserId);

        // check if a student profile is a child of a parent
        Task<bool> IsChildOfParentAsync(string parentUserId, string studentProfileId);

        Task<List<ChildDto>> GetChildrenInfoByParentUserIdAsync(string parentUserId);
    }

}
