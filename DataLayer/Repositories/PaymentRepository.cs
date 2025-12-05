using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories;

public class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
{
    private readonly TpeduContext _context;

    public PaymentRepository(TpeduContext context) : base(context)
    {
        _context = context;
    }

    public Task AddAsync(Payment entity, CancellationToken ct = default)
        => _context.Payments.AddAsync(entity, ct).AsTask();

    public Task<Payment?> GetByOrderIdAsync(PaymentProvider provider, string orderId, CancellationToken ct = default)
        => _context.Payments
            .Include(p => p.Logs)
            .FirstOrDefaultAsync(p => p.Provider == provider && p.OrderId == orderId, ct);

    public Task<Payment?> GetByRequestIdAsync(PaymentProvider provider, string requestId, CancellationToken ct = default)
        => _context.Payments
            .Include(p => p.Logs)
            .FirstOrDefaultAsync(p => p.Provider == provider && p.RequestId == requestId, ct);

    public Task<Payment?> GetLatestPendingPaymentAsync(PaymentProvider provider, CancellationToken ct = default)
        => _context.Payments
            .Include(p => p.Logs)
            .Where(p => p.Provider == provider && p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
}

