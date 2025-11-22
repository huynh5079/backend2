using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Entities
{
    public partial class ClassRequestSchedule : BaseEntity
    {
        //public string Id { get; set; } = null!;

        public string ClassRequestId { get; set; } = null!;

        public byte? DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public virtual ClassRequest ClassRequest { get; set; } = null!;
    }
}
