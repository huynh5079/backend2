using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.Tutors
{
    public class PublicTutorListItemDto
    {
        public string TutorId { get; set; } = default!;
        public string? Username { get; set; }
        public string Email { get; set; } = default!;
        public string TeachingSubjects { get; set; } = default;
        public string TeachingLevel { get; set; }
        public DateTime CreateDate { get; set; }
        public string? AvatarUrl { get; set; } // cho UI tiện hiển thị
    }

    public class PublicTutorDetailDto
    {
        public string TutorId { get; set; } = default!;
        public string TutorProfileId { get; set; } = default!;
        public string? Username { get; set; }
        public string Email { get; set; } = default!;
        public string? AvatarUrl { get; set; }

        // Thông tin cá nhân cơ bản
        public string? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }

        // Học vấn/cơ bản
        public string? EducationLevel { get; set; }
        public string? University { get; set; }
        public string? Major { get; set; }

        // Kinh nghiệm/mô tả chung
        public int? TeachingExperienceYears { get; set; }
        public string? TeachingSubjects { get; set; }
        public string? TeachingLevel { get; set; }
        public string? Bio { get; set; }

        public double? Rating { get; set; }
        public DateTime CreateDate { get; set; }
    }
}
