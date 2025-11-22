using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction;

public interface IClassRepository : IGenericRepository<Class>
{
    Task<List<Class>> GetClassesByTutorAsync(string tutorId);
    Task<List<Class>> GetAvailableClassesAsync();
    Task<PaginationResult<Class>> SearchAndFilterAvailableAsync(
        string? keyword,
        string? subject,
        string? educationLevel,
        ClassMode? mode,
        string? area,
        decimal? minPrice,
        decimal? maxPrice,
        ClassStatus? status,
        int pageNumber,
        int pageSize);
}
