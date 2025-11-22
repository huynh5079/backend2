using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories
{
    public class EscrowRepository : GenericRepository<Escrow>, IEscrowRepository
    {
        private readonly TpeduContext _ctx;
        public EscrowRepository(TpeduContext ctx) : base(ctx)
        {
            _ctx = ctx;
        }

        public Task<Escrow?> GetByIdAsync(string id, CancellationToken ct = default)
            => _ctx.Escrows.SingleOrDefaultAsync(e => e.Id == id, ct);

        public Task AddAsync(Escrow entity, CancellationToken ct = default)
            => _ctx.Escrows.AddAsync(entity, ct).AsTask();
    }
}


