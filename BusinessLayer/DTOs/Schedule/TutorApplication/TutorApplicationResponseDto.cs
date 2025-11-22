using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.TutorApplication
{
    public class TutorApplicationResponseDto
    {
        public string Id { get; set; } // ID của chính TutorApplication
        public string ClassRequestId { get; set; }
        public string Status { get; set; } // "Pending", "Accepted", "Rejected"
        public DateTime AppliedAt { get; set; }
        public string? CoverLetter { get; set; }

        // --- Thông tin Join từ TutorProfile ---
        public string TutorId { get; set; }
        public string TutorName { get; set; }
        public string? TutorAvatarUrl { get; set; }
        public double? TutorRating { get; set; }
        public string? TutorBio { get; set; } // Một đoạn bio ngắn
        public int? TutorTeachingExperienceYears { get; set; }
    }
}
