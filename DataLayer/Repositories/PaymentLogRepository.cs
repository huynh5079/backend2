using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories;

public class PaymentLogRepository : GenericRepository<PaymentLog>, IPaymentLogRepository
{
    private readonly TpeduContext _context;

    public PaymentLogRepository(TpeduContext context) : base(context)
    {
        _context = context;
    }

    public Task AddAsync(PaymentLog entity, CancellationToken ct = default)
        => _context.PaymentLogs.AddAsync(entity, ct).AsTask();
}

