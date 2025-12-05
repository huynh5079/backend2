namespace BusinessLayer.DTOs.Payment;

public class CreateMomoPaymentResponseDto
{
    public string PaymentId { get; set; } = default!;
    public string OrderId { get; set; } = default!;
    public string RequestId { get; set; } = default!;
    public string PayUrl { get; set; } = default!;
    public string? Deeplink { get; set; }
    public string Provider { get; set; } = "MoMo";
}

