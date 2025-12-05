using System;
using System.ComponentModel.DataAnnotations.Schema;
using DataLayer.Enum;

namespace DataLayer.Entities
{
    public partial class Escrow : BaseEntity
    {
        public string ClassId { get; set; } = default!;
        public string ClassAssignId { get; set; } = default!; // Gắn trực tiếp tới việc ghi danh (mỗi học sinh = 1 escrow)
        public string StudentUserId { get; set; } = default!; // UserId của học sinh (thay cho PayerUserId)
        public string TutorUserId { get; set; } = default!; // Required - gia sư của lớp
        
        public decimal GrossAmount { get; set; } // Học phí thô (trước commission) - phần của học sinh này
        [Column(TypeName = "decimal(5,4)")]
        public decimal CommissionRateSnapshot { get; set; } // Snapshot commission rate tại thời điểm tạo
        
        public EscrowStatus Status { get; set; } = EscrowStatus.Held;
        
        // Track đã release/refund bao nhiêu (cho partial release/refund)
        public decimal ReleasedAmount { get; set; } = 0;
        public decimal RefundedAmount { get; set; } = 0;
        
        public DateTime? ReleasedAt { get; set; }
        public DateTime? RefundedAt { get; set; }
        
        // Navigation properties
        public virtual Class? Class { get; set; }
        public virtual ClassAssign? ClassAssign { get; set; }
        public virtual User? StudentUser { get; set; }
        public virtual User? TutorUser { get; set; }
    }
}


