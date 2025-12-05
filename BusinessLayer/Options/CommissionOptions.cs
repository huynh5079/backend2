namespace BusinessLayer.Options;

public class CommissionOptions
{
    // Commission rates (as decimal, e.g., 0.12 = 12%)
    public decimal OneToOneOnline { get; set; } = 0.12m;      // 12%
    public decimal OneToOneOffline { get; set; } = 0.15m;     // 15%
    public decimal GroupClassOnline { get; set; } = 0.10m;   // 10%
    public decimal GroupClassOffline { get; set; } = 0.12m;   // 12%
}

