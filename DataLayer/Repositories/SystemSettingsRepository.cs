using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories
{
    public class SystemSettingsRepository : GenericRepository<SystemSettings>, ISystemSettingsRepository
    {
        private readonly TpeduContext _context;

        public SystemSettingsRepository(TpeduContext context) : base(context)
        {
            _context = context;
        }

        public async Task<SystemSettings?> GetActiveSettingsAsync(CancellationToken ct = default)
        {
            return await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IsActive, ct);
        }

        public async Task<SystemSettings> GetOrCreateSettingsAsync(CancellationToken ct = default)
        {
            var settings = await GetActiveSettingsAsync(ct);
            
            if (settings == null)
            {
                // Tạo mới với giá trị mặc định
                settings = new SystemSettings
                {
                    DepositRate = 0.10m, // 10%
                    IsActive = true
                };
                await _context.SystemSettings.AddAsync(settings, ct);
                await _context.SaveChangesAsync(ct);
            }
            
            return settings;
        }
    }
}

