using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction
{
    public interface ITutorDepositEscrowRepository : IGenericRepository<TutorDepositEscrow>
    {
        Task<TutorDepositEscrow?> GetByIdAsync(string id, CancellationToken ct = default);
        Task<TutorDepositEscrow?> GetByClassIdAsync(string classId, CancellationToken ct = default);
        Task<TutorDepositEscrow?> GetByEscrowIdAsync(string escrowId, CancellationToken ct = default);
        Task AddAsync(TutorDepositEscrow entity, CancellationToken ct = default);
    }
}

