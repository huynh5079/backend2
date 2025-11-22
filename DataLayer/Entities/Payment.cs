using DataLayer.Enum;

namespace DataLayer.Entities;

public class Payment : BaseEntity
{
    public PaymentProvider Provider { get; set; } = PaymentProvider.MoMo;

    public string OrderId { get; set; } = default!;

    public string RequestId { get; set; } = default!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public PaymentContextType ContextType { get; set; }

    public string ContextId { get; set; } = default!;

    public int? ResultCode { get; set; }

    public string? Message { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? ExtraData { get; set; }

    public string? TransactionId { get; set; }

    public ICollection<PaymentLog> Logs { get; set; } = new List<PaymentLog>();
}

