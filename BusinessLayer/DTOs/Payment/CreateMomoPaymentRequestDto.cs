using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Payment;

public class CreateMomoPaymentRequestDto
{
    [Range(1, long.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public PaymentContextType ContextType { get; set; }

    [Required]
    public string ContextId { get; set; } = default!;

    public string? Description { get; set; }

    public string? ExtraData { get; set; }
}

