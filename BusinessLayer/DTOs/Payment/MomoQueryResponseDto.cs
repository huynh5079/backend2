namespace BusinessLayer.DTOs.Payment;

public class MomoQueryResponseDto
{
    public string PaymentId { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public int ResultCode { get; set; }
    public string Message { get; set; } = default!;
    public string? TransId { get; set; }
    public string Status { get; set; } = default!;
    public long Amount { get; set; }
    public long ResponseTime { get; set; }
}

