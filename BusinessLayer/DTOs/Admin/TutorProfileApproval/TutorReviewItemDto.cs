using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.TutorProfileApproval
{
    // Item cho danh sách chờ duyệt
    public class TutorReviewItemDto
    {
        public string UserId { get; set; } = default!;
        public string TutorProfileId { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? UserName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? University { get; set; }
        public string? Major { get; set; }
        public int? TeachingExperienceYears { get; set; }
        public string Status { get; set; } = default!;
    }
}
