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
    public class ClassRequestRepository : GenericRepository<ClassRequest>, IClassRequestRepository
    {
        public ClassRequestRepository(TpeduContext context) : base(context)
        {
            // Chỉ cần gọi constructor của base
        }
        // Che hàm CreateAsync

        public new async Task CreateAsync(ClassRequest entity)
        {
            await _dbSet.AddAsync(entity);
            // KHÔNG GỌI SaveChangesAsync()
        }

        // Che hàm UpdateAsync của base
        public new Task UpdateAsync(ClassRequest entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
            // KHÔNG GỌI SaveChangesAsync()
        }
    }

}
