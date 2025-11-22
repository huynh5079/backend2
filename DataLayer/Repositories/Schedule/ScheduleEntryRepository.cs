using DataLayer.Entities;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Schedule
{
    public class ScheduleEntryRepository : GenericRepository<ScheduleEntry>, IScheduleEntryRepository
    {
        public ScheduleEntryRepository(TpeduContext context) : base(context)
        {
            // Chỉ cần gọi constructor của base
        }
        // Che hàm CreateAsync

        public new async Task CreateAsync(ScheduleEntry entity)
        {
            await _dbSet.AddAsync(entity);
            // KHÔNG GỌI SaveChangesAsync()
        }

        // Che hàm UpdateAsync của base
        public new Task UpdateAsync(ScheduleEntry entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
            // KHÔNG GỌI SaveChangesAsync()
        }

        public async Task<ScheduleEntry?> GetTutorConflictAsync(string tutorProfileId, DateTime startTime, DateTime endTime, string? entryIdToIgnore = null)
        {
            var query = _dbSet.AsNoTracking()
                .Where(se => se.TutorId == tutorProfileId &&
                             se.StartTime < endTime &&
                             se.EndTime > startTime &&
                             se.DeletedAt == null);

            if (!string.IsNullOrEmpty(entryIdToIgnore))
            {
                query = query.Where(se => se.Id != entryIdToIgnore);
            }

            return await query.FirstOrDefaultAsync();
        }
    }
}
