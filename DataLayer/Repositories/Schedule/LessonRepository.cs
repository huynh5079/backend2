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
    public class LessonRepository : GenericRepository<Lesson>, ILessonRepository
    {
        public LessonRepository(TpeduContext context) : base(context)
        {
            // Chỉ cần gọi constructor của base
        }
        // Che hàm CreateAsync

        public new async Task CreateAsync(Lesson entity)
        {
            await _dbSet.AddAsync(entity);
            // KHÔNG GỌI SaveChangesAsync()
        }

        // Che hàm UpdateAsync của base
        public new Task UpdateAsync(Lesson entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
            // KHÔNG GỌI SaveChangesAsync()
        }

        public async Task<(Lesson lesson, Class @class)> GetWithClassAsync(string lessonId)
        {
            var lesson = await _dbSet
                .Include(l => l.Class)
                .FirstOrDefaultAsync(l => l.Id == lessonId)
                ?? throw new KeyNotFoundException("Không tìm thấy buổi học.");

            if (lesson.Class == null)
                throw new InvalidOperationException("Buổi học không gắn lớp.");

            return (lesson, lesson.Class);
        }
    }
}
