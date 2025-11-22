using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction.Schedule
{
    public interface IClassRepository2 : IGenericRepository<Class>
    {
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
}
