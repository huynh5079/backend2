using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities;

/// <summary>
/// System settings - singleton pattern (chỉ có 1 record)
/// Admin có thể cập nhật qua API
/// </summary>
public class SystemSettings : BaseEntity
{
    /// <summary>
    /// Tỷ lệ tiền cọc (% học phí). Ví dụ: 0.10 = 10%
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal DepositRate { get; set; } = 0.10m; // 10% mặc định

    // Chỉ có 1 record duy nhất trong bảng (singleton pattern)
    public bool IsActive { get; set; } = true;
}

