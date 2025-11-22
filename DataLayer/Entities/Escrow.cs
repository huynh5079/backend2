using System;
using DataLayer.Enum;

namespace DataLayer.Entities
{
    public partial class Escrow : BaseEntity
    {
        public string ClassId { get; set; } = default!; // hoặc AssignId tuỳ domain
        public string PayerUserId { get; set; } = default!;
        public string? TutorUserId { get; set; } = default!;
        public decimal GrossAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public EscrowStatus Status { get; set; } = EscrowStatus.Held;
        public DateTime? ReleasedAt { get; set; }
        public DateTime? RefundedAt { get; set; }
        public virtual Class? Class { get; set; }
        public virtual User? PayerUser { get; set; }
        public virtual User? TutorUser { get; set; }
    }
}


