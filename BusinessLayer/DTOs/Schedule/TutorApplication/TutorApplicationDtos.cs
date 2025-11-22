using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.TutorApplication
{
    public class CreateTutorApplicationDto
    {
        [Required]
        public string ClassRequestId { get; set; } = null!;

        // Tutor apply for request, no need cover letter,...
        // Agree with ClassRequest
    }

    public class TutorApplicationResponseDto
    {
        public string Id { get; set; } = null!;
        public string ClassRequestId { get; set; } = null!;
        public string TutorId { get; set; } = null!;
        public ApplicationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? TutorName { get; set; }
        public string? TutorAvatarUrl { get; set; }
    }
}
