using BusinessLayer.DTOs.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.Directory
{
    public class AdminListDtos
    {
        public class TutorListItemDto
        {
            public string TutorId { get; set; } = default!;
            public string? Username { get; set; }
            public string Email { get; set; } = default!;
            public string Status { get; set; } = default!; // from AccountStatus enum -> .ToString()
            public bool IsBanned { get; set; }
            public DateTime CreateDate { get; set; }
        }

        public class StudentListItemDto
        {
            public string StudentId { get; set; } = default!;
            public string? Username { get; set; }
            public string Email { get; set; } = default!;
            public bool IsBanned { get; set; }
            public DateTime CreateDate { get; set; }
        }

        public class ParentListItemDto
        {
            public string ParentId { get; set; } = default!;
            public string? Username { get; set; }
            public string Email { get; set; } = default!;
            public bool IsBanned { get; set; }
            public DateTime CreateDate { get; set; }
        }

        public class AdminStudentDetailDto
        {
            // User info
            public string StudentId { get; set; } = default!;
            public string? Username { get; set; }
            public string Email { get; set; } = default!;
            public string? AvatarUrl { get; set; }
            public string? Phone { get; set; }
            public string? Address { get; set; }
            public string? Gender { get; set; }             // "male"/"female"
            public DateOnly? DateOfBirth { get; set; }
            public string Status { get; set; } = default!;  // AccountStatus
            public bool IsBanned { get; set; }
            public DateTime CreateDate { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public DateTime? BannedAt { get; set; }
            public DateTime? BannedUntil { get; set; }
            public string? BannedReason { get; set; }

            // Student profile
            public string? EducationLevelId { get; set; }
            public string? EducationLevelName { get; set; } // lấy từ navigation EducationLevel
            public string? PreferredSubjects { get; set; }

            // Media: giấy tờ định danh (nếu có)
            public List<MediaItemDto> IdentityDocuments { get; set; } = new();
        }

        public class AdminChildBriefDto
        {
            public string StudentId { get; set; } = default!;
            public string StudentUserId { get; set; } = default!;
            public string? Username { get; set; }
            public string Email { get; set; } = default!;
            public string? AvatarUrl { get; set; }
            public DateTime CreateDate { get; set; }

            public string? Relationship { get; set; }      // từ ParentProfile.Relationship
            public string? EducationLevel { get; set; }    // chuỗi (VD: "Tiểu học")
        }

        public class AdminParentDetailDto
        {
            // User info
            public string ParentId { get; set; } = default!;
            public string? Username { get; set; }
            public string Email { get; set; } = default!;
            public string? AvatarUrl { get; set; }
            public string? Phone { get; set; }
            public string? Address { get; set; }
            public string? Gender { get; set; }            // "male"/"female"
            public DateOnly? DateOfBirth { get; set; }
            public string Status { get; set; } = default!;
            public bool IsBanned { get; set; }
            public DateTime CreateDate { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public DateTime? BannedAt { get; set; }
            public DateTime? BannedUntil { get; set; }
            public string? BannedReason { get; set; }
            public List<AdminChildBriefDto> Children { get; set; } = new();
            // Media: giấy tờ định danh (nếu có)
            public List<MediaItemDto> IdentityDocuments { get; set; } = new();
        }


        // Page DTOs (optional)
        public class AdminStudentListPageDto
        {
            public IEnumerable<StudentListItemDto> Data { get; set; } = new List<StudentListItemDto>();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }

        public class AdminParentListPageDto
        {
            public IEnumerable<ParentListItemDto> Data { get; set; } = new List<ParentListItemDto>();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }
    }
}
