using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace DataLayer.Repositories.Schedule
{
    public class ClassRepository2 : GenericRepository<Class>, IClassRepository2
    {
        public ClassRepository2(TpeduContext context) : base(context) { }

        // hide CreateAsync base
        public new async Task CreateAsync(Class entity)
        {
            await _dbSet.AddAsync(entity);
            // uncall SaveChangesAsync()
        }

        // hide UpdateAsync base
        public new Task UpdateAsync(Class entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
            // uncall SaveChangesAsync()
        }

        public async Task<PaginationResult<Class>> SearchAndFilterAvailableAsync(
            string? keyword,
            string? subject,
            string? educationLevel,
            ClassMode? mode,
            string? area,
            decimal? minPrice,
            decimal? maxPrice,
            ClassStatus? status,
            int pageNumber,
            int pageSize)
        {
            IQueryable<Class> q = _dbSet
                .Include(c => c.Tutor)
                    .ThenInclude(t => t!.User)
                .Include(c => c.ClassSchedules)
                .Where(c => c.Status == ClassStatus.Active && c.CurrentStudentCount < c.StudentLimit);

            // Search by keyword
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordLower = keyword.ToLower();
                q = q.Where(c =>
                    (c.Title != null && c.Title.ToLower().Contains(keywordLower)) ||
                    (c.Description != null && c.Description.ToLower().Contains(keywordLower)) ||
                    (c.Subject != null && c.Subject.ToLower().Contains(keywordLower)) ||
                    (c.Tutor != null && c.Tutor.User != null && 
                     c.Tutor.User.UserName != null && c.Tutor.User.UserName.ToLower().Contains(keywordLower)));
            }

            // Filter by subject
            if (!string.IsNullOrWhiteSpace(subject))
            {
                q = q.Where(c => c.Subject != null && c.Subject.Contains(subject));
            }

            // Filter by education level
            if (!string.IsNullOrWhiteSpace(educationLevel))
            {
                q = q.Where(c => c.EducationLevel != null && c.EducationLevel.Contains(educationLevel));
            }

            // Filter by mode
            if (mode.HasValue)
            {
                q = q.Where(c => c.Mode == mode.Value);
            }

            // Filter by area
            if (!string.IsNullOrWhiteSpace(area))
            {
                var areaLower = area.ToLower();
                q = q.Where(c => c.Tutor != null && 
                               c.Tutor.User != null && 
                               c.Tutor.User.Address != null && 
                               c.Tutor.User.Address.ToLower().Contains(areaLower));
            }

            // Filter by price range
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                q = q.Where(c => c.Price != null &&
                               (!minPrice.HasValue || c.Price >= minPrice.Value) &&
                               (!maxPrice.HasValue || c.Price <= maxPrice.Value));
            }

            // Filter by status
            if (status.HasValue)
            {
                q = q.Where(c => c.Status == status.Value);
            }
            else
            {
                q = q.Where(c => c.Status == ClassStatus.Pending || c.Status == ClassStatus.Active);
            }

            q = q.Include(c => c.ClassSchedules)
                 .Include(c => c.Tutor).ThenInclude(t => t.User);

            // Order by created date
            q = q.OrderByDescending(c => c.CreatedAt);

            var total = await q.CountAsync();
            var items = await q.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

            return new PaginationResult<Class>(items, total, pageNumber, pageSize);
        }
    }
}
