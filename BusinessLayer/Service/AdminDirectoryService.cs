using BusinessLayer.DTOs.Admin.Directory;
using BusinessLayer.DTOs.Media;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BusinessLayer.DTOs.Admin.Directory.AdminListDtos;

namespace BusinessLayer.Service
{
    public class AdminDirectoryService : IAdminDirectoryService
    {
        private readonly IUnitOfWork _uow;
        public AdminDirectoryService(IUnitOfWork uow) => _uow = uow;

        public async Task<PaginationResult<TutorListItemDto>> GetTutorsPagedAsync(int page, int pageSize)
        {
            var result = await _uow.Users.GetPagedByRoleAsync("Tutor", page, pageSize);

            var mapped = result.Data.Select(u => new TutorListItemDto
            {
                TutorId = u.Id,
                Username = u.UserName,
                Email = u.Email!,
                Status = u.Status.ToString(),
                IsBanned = u.IsBanned || u.Status == AccountStatus.Banned,
                CreateDate = u.CreatedAt
            }).ToList();

            return new PaginationResult<TutorListItemDto>(mapped, result.TotalCount, result.PageNumber, result.PageSize);
        }

        public async Task<AdminStudentListPageDto> GetStudentsPagedAsync(int page, int pageSize)
        {
            var result = await _uow.Users.GetPagedByRoleAsync("Student", page, pageSize);
            return new AdminStudentListPageDto
            {
                Data = result.Data.Select(u => new StudentListItemDto
                {
                    StudentId = u.Id,
                    Username = u.UserName,
                    Email = u.Email!,
                    IsBanned = u.IsBanned || u.Status == AccountStatus.Banned,
                    CreateDate = u.CreatedAt
                }).ToList(),
                TotalCount = result.TotalCount,
                Page = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<AdminParentListPageDto> GetParentsPagedAsync(int page, int pageSize)
        {
            var result = await _uow.Users.GetPagedByRoleAsync("Parent", page, pageSize);
            return new AdminParentListPageDto
            {
                Data = result.Data.Select(u => new ParentListItemDto
                {
                    ParentId = u.Id,
                    Username = u.UserName,
                    Email = u.Email!,
                    IsBanned = u.IsBanned || u.Status == AccountStatus.Banned,
                    CreateDate = u.CreatedAt
                }).ToList(),
                TotalCount = result.TotalCount,
                Page = result.PageNumber,
                PageSize = result.PageSize
            };
        }

        public async Task<AdminStudentDetailDto?> GetStudentDetailAsync(string userId)
        {
            var u = await _uow.Users.GetDetailByIdAsync(userId);
            if (u == null || u.RoleName != "Student") return null;

            var sp = await _uow.StudentProfiles.GetByUserIdAsync(userId);

            // Lấy tên bậc học (ưu tiên navigation)
            string? eduName = null;
            //if (sp?.EducationLevelId != null)
            //{
            //    eduName = sp.EducationLevel?.LevelName;
            //    if (eduName == null)
            //    {
            //        var edu = await _uow.EducationLevels.GetByIdAsync(sp.EducationLevelId);
            //        eduName = edu?.LevelName;
            //    }
            //}

            // Identity documents (nếu có)
            var idDocs = await _uow.Media.GetByOwnerAndContextAsync(userId, UploadContext.IdentityDocument);
            var idDocDtos = idDocs.Select(m => new MediaItemDto
            {
                Id = m.Id,
                Url = m.FileUrl,
                FileName = m.FileName,
                ContentType = m.MediaType,
                FileSize = m.FileSize
            }).ToList();

            return new AdminStudentDetailDto
            {
                StudentId = u.Id,
                Username = u.UserName,
                Email = u.Email!,
                AvatarUrl = u.AvatarUrl,
                Phone = u.Phone,
                Address = u.Address,
                Gender = u.Gender?.ToString().ToLowerInvariant(),
                DateOfBirth = u.DateOfBirth,

                Status = u.Status.ToString(),
                IsBanned = u.IsBanned || u.Status == AccountStatus.Banned,
                CreateDate = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                BannedAt = u.BannedAt,
                BannedUntil = u.BannedUntil,
                BannedReason = u.BannedReason,

                //EducationLevelId = sp?.EducationLevelId,
                EducationLevelName = eduName,
                PreferredSubjects = sp?.PreferredSubjects,

                IdentityDocuments = idDocDtos
            };
        }

        public async Task<AdminParentDetailDto?> GetParentDetailAsync(string userId)
        {
            var u = await _uow.Users.GetDetailByIdAsync(userId);
            if (u == null || u.RoleName != "Parent") return null;

            // Lấy tất cả con của parent
            var children = await _uow.ParentProfiles.GetChildrenAllAsync(userId);

            // Identity documents (nếu có)
            var idDocs = await _uow.Media.GetByOwnerAndContextAsync(userId, UploadContext.IdentityDocument);
            var idDocDtos = idDocs.Select(m => new MediaItemDto
            {
                Id = m.Id,
                Url = m.FileUrl,
                FileName = m.FileName,
                ContentType = m.MediaType,
                FileSize = m.FileSize
            }).ToList();

            var dto = new AdminParentDetailDto
            {
                ParentId = u.Id,
                Username = u.UserName,
                Email = u.Email!,
                AvatarUrl = u.AvatarUrl,
                Phone = u.Phone,
                Address = u.Address,
                Gender = u.Gender?.ToString().ToLowerInvariant(),
                DateOfBirth = u.DateOfBirth,

                Status = u.Status.ToString(),
                IsBanned = u.IsBanned || u.Status == AccountStatus.Banned,
                CreateDate = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                BannedAt = u.BannedAt,
                BannedUntil = u.BannedUntil,
                BannedReason = u.BannedReason,

                // ✅ map mảng Children
                Children = children.Select(x => new AdminChildBriefDto
                {
                    StudentId = x.stu.Id,
                    StudentUserId = x.childUser.Id,
                    Username = x.childUser.UserName,
                    Email = x.childUser.Email!,
                    AvatarUrl = x.childUser.AvatarUrl,
                    CreateDate = x.stu.CreatedAt,
                    Relationship = x.link.Relationship,
                    EducationLevel = x.stu.EducationLevel      // chuỗi
                }).ToList(),

                IdentityDocuments = idDocDtos
            };

            return dto;
        }
    }
}
