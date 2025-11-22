using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IReportRepository : IGenericRepository<Report>
    {
        Task<(IReadOnlyList<Report> items, int total)> GetPagedForTutorAsync(string tutorUserId, ReportStatus? status, string? keyword, int page, int pageSize);

        Task<(IReadOnlyList<Report> items, int total)> GetPagedForAdminAsync(ReportStatus? status, string? keyword, int page, int pageSize);

        Task<Report?> GetDetailAsync(string id); // kèm include
    }
}
