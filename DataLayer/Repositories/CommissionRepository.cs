using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataLayer.Entities;
using DataLayer.Repositories.GenericType;
using DataLayer.Repositories.Abstraction;

namespace DataLayer.Repositories;

public class CommissionRepository : GenericRepository<Commission>, ICommissionRepository
{
    private readonly TpeduContext _context;

    public CommissionRepository(TpeduContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Commission?> GetActiveCommissionAsync(CancellationToken ct = default)
    {
        return await _context.Commissions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive, ct);
    }

    public async Task<Commission> GetOrCreateCommissionAsync(CancellationToken ct = default)
    {
        var commission = await GetActiveCommissionAsync(ct);
        
        if (commission == null)
        {
            // Tạo mới với giá trị mặc định
            commission = new Commission
            {
                OneToOneOnline = 0.12m,
                OneToOneOffline = 0.15m,
                GroupClassOnline = 0.10m,
                GroupClassOffline = 0.12m,
                IsActive = true
            };
            await _context.Commissions.AddAsync(commission, ct);
            await _context.SaveChangesAsync(ct);
        }
        
        return commission;
    }
}

