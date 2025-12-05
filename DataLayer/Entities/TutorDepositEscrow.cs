using System;
using System.ComponentModel.DataAnnotations.Schema;
using DataLayer.Enum;

namespace DataLayer.Entities
{
    public partial class TutorDepositEscrow : BaseEntity
    {
        public string ClassId { get; set; } = default!;
        public string? EscrowId { get; set; } // Nullable - Deposit gắn với Class, không phải từng Escrow (với lớp group có nhiều escrow)
        public string TutorUserId { get; set; } = default!;
        public decimal DepositAmount { get; set; }
        /// <summary>
        /// Snapshot của DepositRate tại thời điểm tạo (ví dụ: 0.10 = 10%)
        /// Lưu lại để khi admin thay đổi DepositRate sau này, các khoản cọc cũ vẫn giữ nguyên tỷ lệ ban đầu
        /// </summary>
        [Column(TypeName = "decimal(5,4)")]
        public decimal DepositRateSnapshot { get; set; }
        public TutorDepositStatus Status { get; set; } = TutorDepositStatus.Held;
        public DateTime? RefundedAt { get; set; }
        public DateTime? ForfeitedAt { get; set; }
        public string? ForfeitReason { get; set; } // Lý do bị tịch thu (vi phạm, bỏ dở...)
        
        // Navigation properties
        public virtual Class? Class { get; set; }
        public virtual Escrow? Escrow { get; set; }
        public virtual User? TutorUser { get; set; }
    }
}

