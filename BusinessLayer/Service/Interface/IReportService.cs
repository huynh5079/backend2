using BusinessLayer.Reports;
using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IReportService
    {
        // Tạo report
        Task<string> CreateToTutorAsync(string studentUserId, string lessonId, string mediaId, string reason);
        Task<string> CreateToAdminAsync(string studentUserId, string lessonId, string mediaId, string reason);

        // Tutor xem danh sách report gửi cho mình
        Task<(IReadOnlyList<ReportItemDto> items, int total)> GetForTutorAsync(string tutorUserId, ReportQuery q);

        // Admin xem danh sách report gửi cho mình
        Task<(IReadOnlyList<ReportItemDto> items, int total)> GetForAdminAsync(ReportQuery q);

        // Detail + Update
        Task<ReportDetailDto> GetDetailAsync(string actorUserId, string id, bool isAdmin);
        Task<bool> UpdateStatusAsync(string actorUserId, string id, ReportStatus status, bool isAdmin);
    }
}
