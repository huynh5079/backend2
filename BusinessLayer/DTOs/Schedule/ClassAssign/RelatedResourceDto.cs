using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ClassAssign
{
    public class RelatedResourceDto
    {
        public string ProfileId { get; set; } = default!; // TutorId or StudentId
        public string UserId { get; set; } = default!;    // link profile
        public string FullName { get; set; } = default!;
        public string? AvatarUrl { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}
