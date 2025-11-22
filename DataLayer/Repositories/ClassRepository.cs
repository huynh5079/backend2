using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DataLayer.Repositories;

public class ClassRepository : GenericRepository<Class>, IClassRepository
{
    public ClassRepository(TpeduContext context) : base(context)
    {
    }

    public async Task<List<Class>> GetClassesByTutorAsync(string tutorId)
    {
        return await _context.Classes
            .Include(c => c.Subject)
            .Include(c => c.EducationLevel)
            .Include(c => c.ClassSchedules)
            .Where(c => c.TutorId == tutorId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Class>> GetAvailableClassesAsync()
    {
        return await _context.Classes
            .Include(c => c.Subject)
            .Include(c => c.EducationLevel)
            .Include(c => c.ClassSchedules)
            .Where(c => c.Status == ClassStatus.Active && c.CurrentStudentCount < c.StudentLimit)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
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
        IQueryable<Class> q = _context.Classes
            .Include(c => c.Tutor)
                .ThenInclude(t => t!.User)
            .Include(c => c.ClassSchedules)
            .Where(c => c.Status == ClassStatus.Active && c.CurrentStudentCount < c.StudentLimit);

        // Search by keyword (tên lớp, môn học, mô tả, tên gia sư)
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

        // Filter by area (from Tutor.User.Address)
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

        // Filter by status (nếu muốn filter theo status khác Active)
        if (status.HasValue)
        {
            q = q.Where(c => c.Status == status.Value);
        }

        // Order by created date
        q = q.OrderByDescending(c => c.CreatedAt);

        var total = await q.CountAsync();
        var items = await q.Skip((pageNumber - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync();

        return new PaginationResult<Class>(items, total, pageNumber, pageSize);
    }
}
