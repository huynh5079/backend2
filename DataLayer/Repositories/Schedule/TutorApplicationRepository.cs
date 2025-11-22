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
    public class TutorApplicationRepository : GenericRepository<TutorApplication>, ITutorApplicationRepository
    {
        public TutorApplicationRepository(TpeduContext context) : base(context)
        {
            // Chỉ cần gọi constructor của base
        }
        // Che hàm CreateAsync

        public new async Task CreateAsync(TutorApplication entity)
        {
            await _dbSet.AddAsync(entity);
            // KHÔNG GỌI SaveChangesAsync()
        }

        // Che hàm UpdateAsync của base
        public new Task UpdateAsync(TutorApplication entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
            // KHÔNG GỌI SaveChangesAsync()
        }
    }

}
