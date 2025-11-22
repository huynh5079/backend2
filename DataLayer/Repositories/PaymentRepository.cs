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
}

