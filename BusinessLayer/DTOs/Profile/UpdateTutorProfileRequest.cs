using DataLayer.Enum;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Profile
{
    public class UpdateTutorProfileRequest
    {
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public DateOnly? DateOfBirth { get; set; }

        // Tutor fields
        public string? Bio { get; set; }
        public string? EducationLevel { get; set; }
        public string? University { get; set; }
        public string? Major { get; set; }
        public int? TeachingExperienceYears { get; set; }
        public IEnumerable<string>? TeachingSubjects { get; set; }
        public IEnumerable<string>? TeachingLevel { get; set; }
        public IEnumerable<string>? SpecialSkills { get; set; }

        // Files
        public IFormFile? AvatarFile { get; set; }               // cho phép đổi avatar
        public List<IFormFile>? NewCertificates { get; set; }    // upload thêm certificate
    }
}
