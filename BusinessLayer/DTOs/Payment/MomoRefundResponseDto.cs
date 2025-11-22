namespace BusinessLayer.DTOs.Payment;

public class MomoRefundResponseDto
{
    public string PaymentId { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string RefundId { get; set; } = default!;
    public int ResultCode { get; set; }
    public string Message { get; set; } = default!;
    public long Amount { get; set; }
    public long ResponseTime { get; set; }
}

