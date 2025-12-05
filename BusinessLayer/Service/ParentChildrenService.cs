using BusinessLayer.DTOs.Admin.Parent;
using BusinessLayer.DTOs.Profile;
using BusinessLayer.Helper;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class ParentChildrenService : IParentChildrenService
    {
        private readonly IUnitOfWork _uow;
        public ParentChildrenService(IUnitOfWork uow) => _uow = uow;

        public async Task<PaginationResult<ChildListItemDto>> GetMyChildrenPagedAsync(string parentUserId, int page, int pageSize)
        {
            var rs = await _uow.ParentProfiles.GetChildrenPagedAsync(parentUserId, page, pageSize);

            var mapped = rs.Data.Select(x => new ChildListItemDto
            {
                StudentId = x.stu.Id,
                StudentUserId = x.childUser.Id,
                Username = x.childUser.UserName,
                Email = x.childUser.Email,
                AvatarUrl = x.childUser.AvatarUrl,
                CreateDate = x.stu.CreatedAt,
                Relationship = x.link.Relationship,
                EducationLevel = x.stu.EducationLevel          // ✅ lấy chuỗi trực tiếp
            }).ToList();

            return new PaginationResult<ChildListItemDto>(mapped, rs.TotalCount, rs.PageNumber, rs.PageSize);
        }

        public async Task<ChildDetailDto?> GetChildDetailAsync(string parentUserId, string studentId)
        {
            var link = await _uow.ParentProfiles.GetLinkAsync(parentUserId, studentId);
            if (link == null) return null;

            var stu = await _uow.StudentProfiles.GetByIdAsync(studentId);
            if (stu == null) return null;

            var u = await _uow.Users.GetByIdAsync(stu.UserId!);
            if (u == null) return null;

            return new ChildDetailDto
            {
                StudentId = stu.Id,
                StudentUserId = u.Id,
                Username = u.UserName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                Phone = u.Phone,
                Address = u.Address,
                Gender = u.Gender?.ToString().ToLowerInvariant(),
                DateOfBirth = u.DateOfBirth,

                EducationLevel = stu.EducationLevel,           // ✅ chuỗi
                PreferredSubjects = stu.PreferredSubjects,

                Relationship = link.Relationship,
                CreateDate = stu.CreatedAt,
                UpdatedAt = stu.UpdatedAt
            };
        }

        public async Task<(bool ok, string message, ChildDetailDto? data)> CreateChildAsync(string parentUserId, CreateChildRequest req)
        {
            // 1) Validate parent
            var parent = await _uow.Users.GetByIdAsync(parentUserId);
            if (parent == null || parent.RoleName != "Parent")
                return (false, "tài khoản không hợp lệ", null);

            // 2) Email
            var email = string.IsNullOrWhiteSpace(req.Email)
                ? EmailHelper.GenerateNoEmail(req.Username)
                : req.Email.Trim();

            if (await _uow.Users.ExistsByEmailAsync(email))
                return (false, "email đã tồn tại trong hệ thống", null);

            // 3) Role Student
            var role = await _uow.Roles.GetAsync(r => r.RoleName == RoleEnum.Student);
            if (role == null) return (false, "không tìm thấy role Student", null);

            // 4) Mật khẩu
            var pwd = string.IsNullOrWhiteSpace(req.InitialPassword)
                ? Guid.NewGuid().ToString("N")[..10]
                : req.InitialPassword.Trim();

            // 5) Tạo User con
            var childUser = new User
            {
                UserName = string.IsNullOrWhiteSpace(req.Username) ? "Student" : req.Username.Trim(),
                Email = email,
                PasswordHash = Helper.HashPasswordHelper.HashPassword(pwd),
                Phone = req.Phone,
                Address = req.Address,
                RoleId = role.Id,
                RoleName = role.RoleName.ToString(),
                Status = AccountStatus.Active,
                AvatarUrl = Helper.AvatarHelper.ForStudent(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _uow.Users.CreateAsync(childUser);

            // 6) Tạo StudentProfile (lưu EducationLevel/PreferredSubjects dạng chuỗi)
            var sp = new StudentProfile
            {
                UserId = childUser.Id,
                EducationLevel = req.EducationLevel,          // ✅ chuỗi FE gửi
                PreferredSubjects = req.PreferredSubjects,    // ✅ chuỗi FE gửi
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _uow.StudentProfiles.CreateAsync(sp);

            // 7) Link Parent–Student
            var link = new ParentProfile
            {
                UserId = parentUserId,
                LinkedStudentId = sp.Id,
                Relationship = string.IsNullOrWhiteSpace(req.Relationship) ? "Con" : req.Relationship.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            await _uow.ParentProfiles.CreateAsync(link);

            await _uow.SaveChangesAsync();

            // 8) Trả về detail
            var detail = new ChildDetailDto
            {
                StudentId = sp.Id,
                StudentUserId = childUser.Id,
                Username = childUser.UserName,
                Email = childUser.Email,
                AvatarUrl = childUser.AvatarUrl,
                Phone = childUser.Phone,
                Address = childUser.Address,

                EducationLevel = sp.EducationLevel,           // ✅
                PreferredSubjects = sp.PreferredSubjects,     // ✅

                Relationship = link.Relationship,
                CreateDate = sp.CreatedAt,
                UpdatedAt = sp.UpdatedAt
            };

            return (true, "tạo tài khoản con thành công", detail);
        }

        public async Task<(bool ok, string message)> LinkExistingChildAsync(string parentUserId, LinkExistingChildRequest req)
        {
            var parent = await _uow.Users.GetByIdAsync(parentUserId);
            if (parent == null || parent.RoleName != "Parent")
                return (false, "tài khoản không hợp lệ");

            if (string.IsNullOrWhiteSpace(req.StudentEmail))
                return (false, "thiếu email học sinh");

            var childUser = await _uow.Users.FindByEmailAsync(req.StudentEmail.Trim());
            if (childUser == null)
                return (false, "email không tồn tại trong hệ thống");

            if (childUser.RoleName != "Student")
                return (false, "email này không phải tài khoản học sinh");

            var stuProfile = await _uow.StudentProfiles.GetAsync(s => s.UserId == childUser.Id);
            if (stuProfile == null)
            {
                // Auto-provision nếu HS chưa có profile
                stuProfile = new StudentProfile
                {
                    UserId = childUser.Id,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                await _uow.StudentProfiles.CreateAsync(stuProfile);
            }

            if (await _uow.ParentProfiles.ExistsLinkAsync(parentUserId, stuProfile.Id))
                return (false, "đã liên kết học sinh này rồi");

            var link = new ParentProfile
            {
                UserId = parentUserId,
                LinkedStudentId = stuProfile.Id,
                Relationship = string.IsNullOrWhiteSpace(req.Relationship) ? "Con" : req.Relationship!.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _uow.ParentProfiles.CreateAsync(link);
            await _uow.SaveChangesAsync();

            return (true, "liên kết học sinh thành công");
        }

        public async Task<(bool ok, string message, ChildDetailDto? data)> UpdateChildAsync(string parentUserId, string studentId, UpdateChildRequest req)
        {
            var link = await _uow.ParentProfiles.GetLinkAsync(parentUserId, studentId);
            if (link == null) return (false, "bạn không có quyền cập nhật học sinh này", null);

            var sp = await _uow.StudentProfiles.GetByIdAsync(studentId);
            if (sp == null) return (false, "không tìm thấy student profile", null);

            var u = await _uow.Users.GetByIdAsync(sp.UserId!);
            if (u == null) return (false, "không tìm thấy user của học sinh", null);

            if (!string.IsNullOrWhiteSpace(req.Username)) u.UserName = req.Username.Trim();
            if (!string.IsNullOrWhiteSpace(req.Phone)) u.Phone = req.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(req.Address)) u.Address = req.Address.Trim();
            u.UpdatedAt = DateTime.Now;
            await _uow.Users.UpdateAsync(u);

            if (!string.IsNullOrWhiteSpace(req.EducationLevel)) sp.EducationLevel = req.EducationLevel; // ✅ chuỗi
            if (req.PreferredSubjects != null) sp.PreferredSubjects = req.PreferredSubjects;            // ✅ chuỗi
            sp.UpdatedAt = DateTime.Now;
            await _uow.StudentProfiles.UpdateAsync(sp);

            await _uow.SaveChangesAsync();

            var detail = new ChildDetailDto
            {
                StudentId = sp.Id,
                StudentUserId = u.Id,
                Username = u.UserName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                Phone = u.Phone,
                Address = u.Address,

                EducationLevel = sp.EducationLevel,           // ✅
                PreferredSubjects = sp.PreferredSubjects,     // ✅

                Relationship = link.Relationship,
                CreateDate = sp.CreatedAt,
                UpdatedAt = sp.UpdatedAt
            };
            return (true, "cập nhật học sinh thành công", detail);
        }

        public async Task<(bool ok, string message)> UnlinkChildAsync(string parentUserId, string studentId)
        {
            var link = await _uow.ParentProfiles.GetLinkAsync(parentUserId, studentId);
            if (link == null) return (false, "không tìm thấy liên kết với học sinh này");

            await _uow.ParentProfiles.RemoveAsync(link);
            await _uow.SaveChangesAsync();
            return (true, "đã hủy liên kết học sinh");
        }

        // list all children ids of a parent
        public async Task<List<string>> GetChildrenIdsByParentUserIdAsync(string parentUserId)
        {
            // find all ParentProfile records for the parent
            var parentProfiles = await _uow.ParentProfiles.GetAllAsync(p => p.UserId == parentUserId);

            if (parentProfiles == null || !parentProfiles.Any())
            {
                return new List<string>();
            }

            // take lsit of LinkedStudentId
            // filter null/empty and distinct to avoid duplicates
            var childrenIds = parentProfiles
                .Where(p => !string.IsNullOrEmpty(p.LinkedStudentId))
                .Select(p => p.LinkedStudentId!)
                .Distinct()
                .ToList();

            return childrenIds;
        }

        // check if a student profile is a child of a parent
        public async Task<bool> IsChildOfParentAsync(string parentUserId, string studentProfileId)
        {
            var childrenIds = await GetChildrenIdsByParentUserIdAsync(parentUserId);
            return childrenIds.Contains(studentProfileId);
        }

        // Trong ParentChildrenService
        public async Task<List<ChildDto>> GetChildrenInfoByParentUserIdAsync(string parentUserId)
        {
            // Query bảng ParentProfile, Include sang StudentProfile -> User để lấy tên
            var parentProfiles = await _uow.ParentProfiles.GetAllAsync(
                filter: p => p.UserId == parentUserId && !string.IsNullOrEmpty(p.LinkedStudentId),
                includes: query => query
                    .Include(p => p.LinkedStudent)
                        .ThenInclude(s => s.User) // Include User để lấy FullName
            );

            if (parentProfiles == null || !parentProfiles.Any())
            {
                return new List<ChildDto>();
            }

            // Map sang DTO
            var children = parentProfiles
                .Where(p => p.LinkedStudent != null)
                .Select(p => new ChildDto
                {
                    StudentId = p.LinkedStudentId!,
                    // Ưu tiên lấy FullName, nếu null thì lấy UserName
                    FullName = p.LinkedStudent!.User?.UserName ?? p.LinkedStudent!.User?.Email ?? "Con yêu"
                })
                .GroupBy(c => c.StudentId) // GroupBy để loại trùng lặp (thay vì Distinct)
                .Select(g => g.First())
                .ToList();

            return children;
        }
    }
}
