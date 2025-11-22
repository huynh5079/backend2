namespace DataLayer.Entities;

public class PaymentLog : BaseEntity
{
    public string PaymentId { get; set; } = default!;

    public string Event { get; set; } = default!;

    public string Payload { get; set; } = default!;

    public Payment Payment { get; set; } = default!;
}

