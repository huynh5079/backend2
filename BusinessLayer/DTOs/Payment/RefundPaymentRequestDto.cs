using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Payment;

public class RefundPaymentRequestDto
{
    [Range(1, long.MaxValue)]
    public decimal Amount { get; set; }

    public string? Description { get; set; }
}

