// TUYỆT ĐỐI KHÔNG: using System.Transactions;
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
            _context.Wallets.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
        }
    }
}
