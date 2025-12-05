using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;

namespace BusinessLayer.Service.Interface;

public interface ICommissionManagementService
{
    /// <summary>
    /// Lấy commission settings hiện tại
    /// </summary>
    Task<CommissionDto> GetCommissionAsync(CancellationToken ct = default);

    /// <summary>
    /// Cập nhật commission settings
    /// </summary>
    Task<CommissionDto> UpdateCommissionAsync(UpdateCommissionDto dto, CancellationToken ct = default);
}

