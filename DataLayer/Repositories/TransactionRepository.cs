// TUYỆT ĐỐI KHÔNG: using System.Transactions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataLayer.Repositories.Abstraction;
using TransactionEntity = DataLayer.Entities.Transaction;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories
{
    public class TransactionRepository : GenericRepository<TransactionEntity>, ITransactionRepository
    {
        private readonly TpeduContext _context;

        public TransactionRepository(TpeduContext context) : base(context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<TransactionEntity> items, int total)>
            GetByWalletIdAsync(string walletId, int page, int size, CancellationToken ct = default)
        {
            var query = _context.Transactions.AsNoTracking()
                         .Where(t => t.WalletId == walletId)
                         .OrderByDescending(t => t.CreatedAt); // CreatedAt từ BaseEntity

            var total = await query.CountAsync(ct);
            var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
            return (items, total);
        }

        public Task AddAsync(TransactionEntity entity, CancellationToken ct = default)
            => _context.Transactions.AddAsync(entity, ct).AsTask(); // 👈 AddAsync cụ thể
    }
}
