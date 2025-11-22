using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction;

public interface IPaymentLogRepository : IGenericRepository<PaymentLog>
{
    Task AddAsync(PaymentLog entity, CancellationToken ct = default);
}

