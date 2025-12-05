namespace BusinessLayer.DTOs.Payment;

public class PaymentStatusDto
{
    public string PaymentId { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string RequestId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!; // Pending, Paid, Failed, Expired, Refunded
    public string? Message { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasTransaction { get; set; } // Đã có transaction chưa (đã cộng tiền chưa)
}

