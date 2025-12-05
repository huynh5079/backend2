using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction
{
    public interface ISystemSettingsRepository : IGenericRepository<SystemSettings>
    {
        Task<SystemSettings?> GetActiveSettingsAsync(CancellationToken ct = default);
        Task<SystemSettings> GetOrCreateSettingsAsync(CancellationToken ct = default);
    }
}
