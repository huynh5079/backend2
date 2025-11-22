using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories;

public class ClassScheduleRepository : GenericRepository<ClassSchedule>, IClassScheduleRepository
{
    public ClassScheduleRepository(TpeduContext context) : base(context)
    {
    }

    public async Task<List<ClassSchedule>> GetSchedulesByClassIdAsync(string classId)
    {
        return await _context.ClassSchedules
            .Where(cs => cs.ClassId == classId)
            .ToListAsync();
    }
}
