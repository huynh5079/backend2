using BusinessLayer.DTOs.Media;
using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Profile
{
    public class UserBasicDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class StudentProfileDto : UserBasicDto
    {
        public string? EducationLevel { get; set; }
        public string? PreferredSubjects { get; set; }
    }

    public class ParentChildBriefDto
    {
        public string StudentId { get; set; } = default!;
        public string StudentUserId { get; set; } = default!;
        public string? Username { get; set; }
        public string Email { get; set; } = default!;
        public string? AvatarUrl { get; set; }
        public DateTime CreateDate { get; set; }

        public string? Relationship { get; set; }
        public string? EducationLevel { get; set; }
    }

    public class ParentProfileDto : UserBasicDto
    {
        public List<ParentChildBriefDto> Children { get; set; } = new();
    }

    public class TutorProfileDto : UserBasicDto
    {
        public string TutorProfileId { get; set; }
        public string? Bio { get; set; }
        public string? EducationLevel { get; set; }
        public string? University { get; set; }
        public string? Major { get; set; }
        public int? TeachingExperienceYears { get; set; }
        public string? TeachingSubjects { get; set; }
        public string? TeachingLevel { get; set; }
        public string? SpecialSkills { get; set; }
        public double? Rating { get; set; }

        // Chứng chỉ/CMND… (nếu bạn cần show)
        public List<MediaItemDto> Certificates { get; set; } = new();
        public List<MediaItemDto> IdentityDocuments { get; set; } = new();
    }
}
