using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction;

public interface IClassScheduleRepository : IGenericRepository<ClassSchedule>
{
    Task<List<ClassSchedule>> GetSchedulesByClassIdAsync(string classId);
}
