using DataLayer.Enum;

namespace BusinessLayer.DTOs.Schedule.Class
{
    /// <summary>
    /// DTO cho Admin cancel class request body (không có ClassId vì lấy từ route)
    /// </summary>
    public class CancelClassRequestBodyDto
    {
        public ClassCancelReason Reason { get; set; }
        public string? Note { get; set; } // Ghi chú lý do hủy chi tiết
        
        // Optional: Nếu chỉ hủy 1 học sinh (cho group class)
        public string? StudentId { get; set; } // Nếu null = hủy cả lớp, nếu có = chỉ hủy 1 HS
    }

    /// <summary>
    /// DTO cho Admin cancel class request (có ClassId từ route)
    /// </summary>
    public class CancelClassRequestDto
    {
        public string ClassId { get; set; } = default!;
        public ClassCancelReason Reason { get; set; }
        public string? Note { get; set; }
        public string? StudentId { get; set; }
    }

    /// <summary>
    /// DTO cho Tutor cancel class request
    /// </summary>
    public class CancelClassByTutorRequestDto
    {
        public string? Reason { get; set; } // Lý do hủy (optional)
    }

    /// <summary>
    /// Response khi cancel class
    /// </summary>
    public class CancelClassResponseDto
    {
        public string ClassId { get; set; } = default!;
        public ClassStatus NewStatus { get; set; }
        public ClassCancelReason Reason { get; set; }
        public int RefundedEscrowsCount { get; set; } // Số escrow đã refund
        public decimal TotalRefundedAmount { get; set; } // Tổng tiền đã refund
        public bool DepositRefunded { get; set; } // Deposit đã hoàn chưa
        public decimal? DepositRefundAmount { get; set; } // Số tiền deposit đã hoàn
        public string? Message { get; set; }
    }
}

