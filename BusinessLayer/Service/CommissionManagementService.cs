using System;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using DataLayer.Repositories.Abstraction;

namespace BusinessLayer.Service;

public class CommissionManagementService : ICommissionManagementService
{
    private readonly IUnitOfWork _uow;

    public CommissionManagementService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<CommissionDto> GetCommissionAsync(CancellationToken ct = default)
    {
        var commission = await _uow.Commissions.GetOrCreateCommissionAsync(ct);
        
        return new CommissionDto
        {
            Id = commission.Id,
            OneToOneOnline = commission.OneToOneOnline,
            OneToOneOffline = commission.OneToOneOffline,
            GroupClassOnline = commission.GroupClassOnline,
            GroupClassOffline = commission.GroupClassOffline,
            IsActive = commission.IsActive,
            CreatedAt = commission.CreatedAt,
            UpdatedAt = commission.UpdatedAt
        };
    }

    public async Task<CommissionDto> UpdateCommissionAsync(UpdateCommissionDto dto, CancellationToken ct = default)
    {
        var commission = await _uow.Commissions.GetOrCreateCommissionAsync(ct);

        // Chỉ cập nhật các field được truyền vào
        if (dto.OneToOneOnline.HasValue)
            commission.OneToOneOnline = dto.OneToOneOnline.Value;
        
        if (dto.OneToOneOffline.HasValue)
            commission.OneToOneOffline = dto.OneToOneOffline.Value;
        
        if (dto.GroupClassOnline.HasValue)
            commission.GroupClassOnline = dto.GroupClassOnline.Value;
        
        if (dto.GroupClassOffline.HasValue)
            commission.GroupClassOffline = dto.GroupClassOffline.Value;

        commission.UpdatedAt = DateTime.UtcNow;

        await _uow.Commissions.UpdateAsync(commission);
        await _uow.SaveChangesAsync();

        return new CommissionDto
        {
            Id = commission.Id,
            OneToOneOnline = commission.OneToOneOnline,
            OneToOneOffline = commission.OneToOneOffline,
            GroupClassOnline = commission.GroupClassOnline,
            GroupClassOffline = commission.GroupClassOffline,
            IsActive = commission.IsActive,
            CreatedAt = commission.CreatedAt,
            UpdatedAt = commission.UpdatedAt
        };
    }
}

