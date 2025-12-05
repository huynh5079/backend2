using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction;

public interface ICommissionRepository : IGenericRepository<Commission>
{
    /// <summary>
    /// Lấy commission settings đang active (chỉ có 1 record)
    /// </summary>
    Task<Commission?> GetActiveCommissionAsync(CancellationToken ct = default);

    /// <summary>
    /// Tạo hoặc cập nhật commission settings (singleton pattern)
    /// </summary>
    Task<Commission> GetOrCreateCommissionAsync(CancellationToken ct = default);
}

