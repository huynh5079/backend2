using System;

namespace BusinessLayer.DTOs.Wallet;

public class CommissionDto
{
    public string Id { get; set; } = default!;
    public decimal OneToOneOnline { get; set; }
    public decimal OneToOneOffline { get; set; }
    public decimal GroupClassOnline { get; set; }
    public decimal GroupClassOffline { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateCommissionDto
{
    public decimal? OneToOneOnline { get; set; }
    public decimal? OneToOneOffline { get; set; }
    public decimal? GroupClassOnline { get; set; }
    public decimal? GroupClassOffline { get; set; }
}

