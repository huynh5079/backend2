using System;

namespace BusinessLayer.DTOs.Wallet
{
    public class PayEscrowRequest
    {
        public string ClassId { get; set; } = default!;
        // GrossAmount và CommissionRate đã bị loại bỏ - lấy từ DB để bảo mật
        // GrossAmount sẽ lấy từ Class.Price trong database
        // CommissionRate sẽ tự động tính từ Commission rules
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

    public class PartialReleaseEscrowRequest
    {
        public string EscrowId { get; set; } = default!;
        public decimal ReleasePercentage { get; set; } // Phần trăm cần release (0.0 - 1.0), ví dụ: 0.5 = 50%
    }

    public class PartialRefundEscrowRequest
    {
        public string EscrowId { get; set; } = default!;
        public decimal RefundPercentage { get; set; } // Phần trăm cần refund (0.0 - 1.0), ví dụ: 0.8 = 80%
    }

    public class ProcessTutorDepositRequest
    {
        public string ClassId { get; set; } = default!;
        // DepositAmount đã bị loại bỏ - gia sư không thể tự chỉnh, luôn dùng giá trị mặc định từ config
    }

    public class ForfeitDepositRequest
    {
        public string TutorDepositEscrowId { get; set; } = default!;
        public string Reason { get; set; } = default!;
        public bool RefundToStudent { get; set; } = true; // true: trả về student, false: giữ lại cho system
    }

    public class SystemSettingsDto
    {
        public string Id { get; set; } = default!;
        public decimal DepositRate { get; set; } // Tỷ lệ % (ví dụ: 0.10 = 10%)
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateDepositSettingsDto
    {
        public decimal? DepositRate { get; set; } // Optional: Tỷ lệ % (ví dụ: 0.10 = 10%)
    }

    public class PayEscrowResponse
    {
        public string EscrowId { get; set; } = default!;
        public string ClassId { get; set; } = default!;
        public string ClassAssignId { get; set; } = default!;
        public string StudentUserId { get; set; } = default!;
        public decimal GrossAmount { get; set; }
        public decimal CommissionRateSnapshot { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetAmount { get; set; }
        public string Status { get; set; } = default!;
    }

    public class ProcessTutorDepositResponse
    {
        public string TutorDepositEscrowId { get; set; } = default!;
        public string ClassId { get; set; } = default!;
        public string EscrowId { get; set; } = default!;
        public decimal DepositAmount { get; set; }
        public decimal DepositRateSnapshot { get; set; }
        public string Status { get; set; } = default!;
    }

    public class ForfeitDepositResponse
    {
        public string TutorDepositEscrowId { get; set; } = default!;
        public string ClassId { get; set; } = default!;
        public decimal DepositAmount { get; set; }
        public bool RefundedToStudent { get; set; }
        public string Status { get; set; } = default!;
    }
}


