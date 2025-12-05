using System;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using DataLayer.Repositories.Abstraction;

namespace BusinessLayer.Service
{
    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly IUnitOfWork _uow;

        public SystemSettingsService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default)
        {
            var settings = await _uow.SystemSettings.GetOrCreateSettingsAsync(ct);
            
            return new SystemSettingsDto
            {
                Id = settings.Id,
                DepositRate = settings.DepositRate,
                IsActive = settings.IsActive,
                CreatedAt = settings.CreatedAt,
                UpdatedAt = settings.UpdatedAt
            };
        }

        public async Task<SystemSettingsDto> UpdateDepositSettingsAsync(UpdateDepositSettingsDto dto, CancellationToken ct = default)
        {
            var settings = await _uow.SystemSettings.GetOrCreateSettingsAsync(ct);

            // Chỉ cập nhật tỷ lệ % nếu được truyền vào
            if (dto.DepositRate.HasValue)
            {
                if (dto.DepositRate.Value <= 0 || dto.DepositRate.Value > 1)
                    throw new ArgumentException("Tỷ lệ cọc phải từ 0 đến 1 (0% đến 100%)");
                settings.DepositRate = dto.DepositRate.Value;
            }

            settings.UpdatedAt = DateTime.UtcNow;

            await _uow.SystemSettings.UpdateAsync(settings);
            await _uow.SaveChangesAsync();

            return new SystemSettingsDto
            {
                Id = settings.Id,
                DepositRate = settings.DepositRate,
                IsActive = settings.IsActive,
                CreatedAt = settings.CreatedAt,
                UpdatedAt = settings.UpdatedAt
            };
        }
    }
}

