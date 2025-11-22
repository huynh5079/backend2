using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Entities
{
    public partial class AvailabilityBlock : BaseEntity
    {
        //public string Id { get; set; } = null!;

        public string TutorId { get; set; } = null!;

        public string Title { get; set; } = null!;

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public string? Notes { get; set; }

        public virtual ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();

        public virtual TutorProfile Tutor { get; set; } = null!;
    }
}
