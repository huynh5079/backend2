using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    Task AddAsync(Payment entity, CancellationToken ct = default);

    Task<Payment?> GetByOrderIdAsync(PaymentProvider provider, string orderId, CancellationToken ct = default);
    
    Task<Payment?> GetByRequestIdAsync(PaymentProvider provider, string requestId, CancellationToken ct = default);
    
    Task<Payment?> GetLatestPendingPaymentAsync(PaymentProvider provider, CancellationToken ct = default);
}

