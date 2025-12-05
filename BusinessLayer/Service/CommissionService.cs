using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;

namespace BusinessLayer.Service;

public class CommissionService : ICommissionService
{
    private readonly IUnitOfWork _uow;

    public CommissionService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<decimal> CalculateCommissionRateAsync(Class classEntity, int? numberOfLessons = null, CancellationToken ct = default)
    {
        var commissionType = DetermineCommissionType(classEntity, numberOfLessons);
        return await GetCommissionRateAsync(commissionType, ct);
    }

    public CommissionType DetermineCommissionType(Class classEntity, int? numberOfLessons = null)
    {
        // Commission được tính dựa trên StudentLimit và Mode
        // Học sinh phải thanh toán toàn bộ trước, hệ thống giữ escrow
        
        // Phân biệt 1-1 vs nhiều học sinh
        bool isOneToOne = classEntity.StudentLimit == 1;

        // Phân biệt Online vs Offline
        bool isOnline = classEntity.Mode == ClassMode.Online;

        if (isOneToOne)
        {
            // Lớp 1-1: Online 12%, Offline 15%
            return isOnline ? CommissionType.OneToOneOnline : CommissionType.OneToOneOffline;
        }
        else
        {
            // Lớp nhóm: Online 10%, Offline 12%
            return isOnline ? CommissionType.GroupClassOnline : CommissionType.GroupClassOffline;
        }
    }

    public async Task<decimal> GetCommissionRateAsync(CommissionType commissionType, CancellationToken ct = default)
    {
        var commission = await _uow.Commissions.GetOrCreateCommissionAsync(ct);
        
        return commissionType switch
        {
            CommissionType.OneToOneOnline => commission.OneToOneOnline,
            CommissionType.OneToOneOffline => commission.OneToOneOffline,
            CommissionType.GroupClassOnline => commission.GroupClassOnline,
            CommissionType.GroupClassOffline => commission.GroupClassOffline,
            _ => commission.GroupClassOnline // Default fallback
        };
    }

    public decimal GetCommissionRate(CommissionType commissionType)
    {
        // Synchronous version - không nên dùng, giữ lại để backward compatibility
        // Nên dùng GetCommissionRateAsync thay thế
        return GetCommissionRateAsync(commissionType).GetAwaiter().GetResult();
    }
}

