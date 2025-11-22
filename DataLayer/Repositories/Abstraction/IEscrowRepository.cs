using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction
{
    public interface IEscrowRepository : IGenericRepository<Escrow>
    {
        Task<Escrow?> GetByIdAsync(string id, CancellationToken ct = default);
        Task AddAsync(Escrow entity, CancellationToken ct = default);
    }
}


