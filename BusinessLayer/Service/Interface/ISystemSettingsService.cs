using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;

namespace BusinessLayer.Service.Interface
{
    public interface ISystemSettingsService
    {
        Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default);
        Task<SystemSettingsDto> UpdateDepositSettingsAsync(UpdateDepositSettingsDto dto, CancellationToken ct = default);
    }
}

