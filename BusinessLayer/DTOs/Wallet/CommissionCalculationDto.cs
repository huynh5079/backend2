using DataLayer.Enum;

namespace BusinessLayer.DTOs.Wallet;

public class CommissionCalculationDto
{
    public string ClassId { get; set; } = default!;
    public string ClassTitle { get; set; } = string.Empty;
    public CommissionType CommissionType { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetAmount { get; set; }
}

