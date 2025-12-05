using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities;

public class Commission : BaseEntity
{
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal OneToOneOnline { get; set; } = 0.12m;      // 12%

    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal OneToOneOffline { get; set; } = 0.15m;     // 15%

    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal GroupClassOnline { get; set; } = 0.10m;    // 10%

    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal GroupClassOffline { get; set; } = 0.12m;   // 12%


    // Chỉ có 1 record duy nhất trong bảng (singleton pattern)
    public bool IsActive { get; set; } = true;
}

