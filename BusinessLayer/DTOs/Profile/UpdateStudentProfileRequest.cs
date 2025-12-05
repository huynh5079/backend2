using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Profile
{
    public class UpdateStudentProfileRequest
    {
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public DateOnly? DateOfBirth { get; set; }

        // Student fields
        public string? EducationLevel { get; set; }
        public string? PreferredSubjects { get; set; }
    }
}
