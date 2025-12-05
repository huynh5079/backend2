using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Enum;

namespace BusinessLayer.Service.Interface;

public interface ICommissionService
{
    /// <summary>
    /// Tính commission rate tự động dựa trên class properties và số buổi học
    /// Học sinh phải thanh toán toàn bộ trước, hệ thống giữ escrow, sau đó tính commission khi release
    /// </summary>
    Task<decimal> CalculateCommissionRateAsync(Class classEntity, int? numberOfLessons = null, CancellationToken ct = default);

    /// <summary>
    /// Xác định CommissionType từ class properties (StudentLimit và Mode)
    /// - OneToOneOnline: 12%
    /// - OneToOneOffline: 15%
    /// - GroupClassOnline: 10%
    /// - GroupClassOffline: 12%
    /// </summary>
    CommissionType DetermineCommissionType(Class classEntity, int? numberOfLessons = null);

    /// <summary>
    /// Lấy commission rate từ CommissionType (async - đọc từ DB)
    /// </summary>
    Task<decimal> GetCommissionRateAsync(CommissionType commissionType, CancellationToken ct = default);

    /// <summary>
    /// Lấy commission rate từ CommissionType (sync - backward compatibility)
    /// </summary>
    decimal GetCommissionRate(CommissionType commissionType);
}

