using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Payment;

public class CreateMomoPaymentRequestDto
{
    [Range(1, long.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public PaymentContextType ContextType { get; set; }

    /// <summary>
    /// ContextId is required for Escrow, optional for WalletDeposit (will use userId if not provided)
    /// </summary>
    public string? ContextId { get; set; }

    public string? Description { get; set; }

    public string? ExtraData { get; set; }
}

