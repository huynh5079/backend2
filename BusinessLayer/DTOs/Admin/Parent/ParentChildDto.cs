using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.Parent
{
    public class CreateChildRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }            // optional
        public string? Phone { get; set; }            // optional
        public string? Address { get; set; }          // optional
        public string? EducationLevel { get; set; }   // ✅ đổi: lưu chuỗi trực tiếp
        public string? PreferredSubjects { get; set; }// optional (chuỗi)
        public string? Relationship { get; set; }     // "Con" | "Cha/Con"...
        public string? InitialPassword { get; set; }  // optional
    }

    public class LinkExistingChildRequest
    {
        public string? StudentEmail { get; set; }
        public string? Relationship { get; set; }
    }

    public class UpdateChildRequest
    {
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? EducationLevel { get; set; }   // ✅ đổi
        public string? PreferredSubjects { get; set; }// ✅ vẫn là chuỗi
    }

    public class ChildListItemDto
    {
        public string StudentId { get; set; } = default!;
        public string StudentUserId { get; set; } = default!;
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime CreateDate { get; set; }
        public string? Relationship { get; set; }
        public string? EducationLevel { get; set; }   // ✅ gọn còn một field
    }

    public class ChildDetailDto
    {
        public string StudentId { get; set; } = default!;
        public string StudentUserId { get; set; } = default!;
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }

        public string? EducationLevel { get; set; }   // ✅ gọn còn một field
        public string? PreferredSubjects { get; set; }

        public string? Relationship { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
