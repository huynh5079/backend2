using BusinessLayer.DTOs.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.TutorProfileApproval
{
    public class TutorReviewDetailDto
    {
        public string UserId { get; set; } = default!;
        public string TutorProfileId { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? UserName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public string? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }

        // Tutor info
        public string? Bio { get; set; }
        public string? ExperienceDetails { get; set; }
        public string? University { get; set; }
        public string? Major { get; set; }
        public string? EducationLevel { get; set; }
        public int? TeachingExperienceYears { get; set; }
        public string? TeachingSubjects { get; set; }
        public string? TeachingLevel { get; set; }

        public string Status { get; set; } = default!;
        public string? ReviewStatus { get; set; }
        public string? RejectReason { get; set; }
        public string? ProvideNote { get; set; }

        public List<MediaItemDto> IdentityDocuments { get; set; } = new();
        public List<MediaItemDto> Certificates { get; set; } = new();
    }

}
