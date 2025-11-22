using System.Threading;
using System.Threading.Tasks;
using DataLayer.Repositories.GenericType.Abstraction;
using WalletEntity = DataLayer.Entities.Wallet;

namespace DataLayer.Repositories.Abstraction
{
    public interface IWalletRepository : IGenericRepository<WalletEntity>
    {
        Task<WalletEntity?> GetByUserIdAsync(string userId, CancellationToken ct = default);
        Task AddAsync(WalletEntity entity, CancellationToken ct = default);
        Task Update(WalletEntity entity);
    }
}
