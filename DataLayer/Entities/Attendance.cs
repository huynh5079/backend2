using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Entities
{
    public partial class Attendance : BaseEntity
    {
        //public int AttendanceId { get; set; }

        public string LessonId { get; set; } = null!;

        public string StudentId { get; set; } = null!;

        public AttendanceStatus Status { get; set; }

        public string? Notes { get; set; }

        public virtual Lesson Lesson { get; set; } = null!;

        public virtual StudentProfile Student { get; set; } = null!;
    }
}
