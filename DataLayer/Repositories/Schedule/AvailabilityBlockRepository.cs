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
    public class AvailabilityBlockRepository : GenericRepository<AvailabilityBlock>, IAvailabilityBlockRepository
    {
        public AvailabilityBlockRepository(TpeduContext context) : base(context)
        {
        }

        public new async Task CreateAsync(AvailabilityBlock entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public new Task UpdateAsync(AvailabilityBlock entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public new Task RemoveAsync(AvailabilityBlock entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }
    }

}
