// TUYỆT ĐỐI KHÔNG: using System.Transactions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataLayer.Repositories.Abstraction;
using WalletEntity = DataLayer.Entities.Wallet;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories
{
    public class WalletRepository : GenericRepository<WalletEntity>, IWalletRepository
    {
        private readonly TpeduContext _context;

        public WalletRepository(TpeduContext context) : base(context)
        {
            _context = context;
        }

        public Task<WalletEntity?> GetByUserIdAsync(string userId, CancellationToken ct = default)
            => _context.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.UserId == userId, ct);

        public Task AddAsync(WalletEntity entity, CancellationToken ct = default)
            => _context.Wallets.AddAsync(entity, ct).AsTask(); 

        
        public new Task Update(WalletEntity entity)
        {
            // Kiểm tra xem entity đã được track chưa
            var trackedEntity = _context.Wallets.Local.FirstOrDefault(e => e.Id == entity.Id);
            if (trackedEntity != null)
            {
                // Entity đã được track, cập nhật giá trị từ entity mới
                // QUAN TRỌNG: Cập nhật giá trị trên tracked entity
                trackedEntity.Balance = entity.Balance;
                trackedEntity.IsFrozen = entity.IsFrozen;
                trackedEntity.Currency = entity.Currency;
                // Đảm bảo state là Modified để EF biết cần update
                _context.Entry(trackedEntity).State = EntityState.Modified;
            }
            else
            {
                // Entity chưa được track, attach và set state
                // QUAN TRỌNG: Attach entity với giá trị mới, sau đó set state = Modified
                _context.Wallets.Attach(entity);
                _context.Entry(entity).State = EntityState.Modified;
            }
            return Task.CompletedTask;
        }
    }
}
