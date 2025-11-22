using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Entities
{
    public partial class ScheduleEntry : BaseEntity
    {
        // public string ScheduleId { get; set; }

        public string TutorId { get; set; } = null!;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public EntryType EntryType { get; set; }

        public string? LessonId { get; set; }

        public string? BlockId { get; set; }

        public virtual AvailabilityBlock? Block { get; set; }

        public virtual Lesson? Lesson { get; set; }

        public virtual TutorProfile Tutor { get; set; } = null!;
    }
}
