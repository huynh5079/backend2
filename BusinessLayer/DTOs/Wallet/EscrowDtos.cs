using System;

namespace BusinessLayer.DTOs.Wallet
{
    public class PayEscrowRequest
    {
        public string ClassId { get; set; } = default!;
        public decimal GrossAmount { get; set; }
        public decimal CommissionRate { get; set; } // 0..1 or percent (10 => 10%) tuỳ quy ước
        public string? PayerStudentUserId { get; set; } // nếu parent trả thay
    }

    public class ReleaseEscrowRequest
    {
        public string EscrowId { get; set; } = default!;
    }

    public class RefundEscrowRequest
    {
        public string EscrowId { get; set; } = default!;
    }
}


