using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories
{
    public class TutorDepositEscrowRepository : GenericRepository<TutorDepositEscrow>, ITutorDepositEscrowRepository
    {
        private readonly TpeduContext _ctx;
        public TutorDepositEscrowRepository(TpeduContext ctx) : base(ctx)
        {
            _ctx = ctx;
        }

        public Task<TutorDepositEscrow?> GetByIdAsync(string id, CancellationToken ct = default)
            => _ctx.TutorDepositEscrows.SingleOrDefaultAsync(e => e.Id == id, ct);

        public Task<TutorDepositEscrow?> GetByClassIdAsync(string classId, CancellationToken ct = default)
            => _ctx.TutorDepositEscrows
                .Where(e => e.ClassId == classId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);

        public Task<TutorDepositEscrow?> GetByEscrowIdAsync(string escrowId, CancellationToken ct = default)
            => _ctx.TutorDepositEscrows.SingleOrDefaultAsync(e => e.EscrowId == escrowId, ct);

        public Task AddAsync(TutorDepositEscrow entity, CancellationToken ct = default)
            => _ctx.TutorDepositEscrows.AddAsync(entity, ct).AsTask();
    }
}

