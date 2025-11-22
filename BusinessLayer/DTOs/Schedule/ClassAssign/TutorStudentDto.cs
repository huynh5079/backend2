using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ClassAssign
{
    public class TutorStudentDto
    {
        public string StudentId { get; set; } = default!;       // ProfileId
        public string StudentUserId { get; set; } = default!;   // UserId
        public string StudentName { get; set; } = default!;
        public string? StudentAvatarUrl { get; set; }
        public string? StudentPhone { get; set; }
        public string? StudentEmail { get; set; }

        // class info
        public string ClassId { get; set; } = default!;
        public string ClassTitle { get; set; } = default!;
        public int StudentLimit { get; set; } // 1 - 1 or Group class
        public DateTime? JoinedAt { get; set; }
    }
}
