using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Entities
{
    public partial class ClassAssign : BaseEntity
    {
        // public string EnrollmentId { get; set; }

        public string? ClassId { get; set; }

        public string? StudentId { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public ApprovalStatus ApprovalStatus { get; set; }

        public DateTime? EnrolledAt { get; set; }

        public virtual Class? Class { get; set; }

        public virtual StudentProfile? Student { get; set; }
    }
}
