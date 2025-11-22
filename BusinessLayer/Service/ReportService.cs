using BusinessLayer.Reports;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _uow;

        public ReportService(IUnitOfWork uow) => _uow = uow;

        // map helpers
        private static ReportItemDto MapItem(Report r) => new()
        {
            Id = r.Id,
            ReporterId = r.ReporterId,
            TargetUserId = r.TargetUserId,
            TargetLessonId = r.TargetLessonId,
            TargetMediaId = r.TargetMediaId,
            Description = r.Description,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        };

        private static ReportDetailDto MapDetail(Report r) => new()
        {
            Id = r.Id,
            ReporterId = r.ReporterId,
            TargetUserId = r.TargetUserId,
            TargetLessonId = r.TargetLessonId,
            TargetMediaId = r.TargetMediaId,
            Description = r.Description,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ReporterEmail = r.Reporter?.Email,
            TargetUserEmail = r.TargetUser?.Email,
            LessonTitle = r.TargetLesson?.Title,
            MediaFileName = r.TargetMedia?.FileName
        };

        public async Task<string> CreateToTutorAsync(string studentUserId, string lessonId, string mediaId, string reason)
            => await CreateCoreAsync(studentUserId, lessonId, mediaId, reason, routeToAdmin: false);

        public async Task<string> CreateToAdminAsync(string studentUserId, string lessonId, string mediaId, string reason)
            => await CreateCoreAsync(studentUserId, lessonId, mediaId, reason, routeToAdmin: true);

        private async Task<string> CreateCoreAsync(string studentUserId, string lessonId, string mediaId, string reason, bool routeToAdmin)
        {
            // Validate lesson, class, enrollment (để ở repo qua uow)
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);

            var studentProfileId = await _uow.StudentProfiles.GetIdByUserIdAsync(studentUserId)
                ?? throw new UnauthorizedAccessException("Tài khoản học sinh không hợp lệ.");

            var isEnrolled = await _uow.ClassAssigns.IsApprovedAsync(cls.Id, studentProfileId);
            if (!isEnrolled) throw new UnauthorizedAccessException("Bạn không thuộc lớp này.");

            var media = await _uow.Media.GetAsync(m => m.Id == mediaId &&
                                                       m.LessonId == lessonId &&
                                                       m.Context == UploadContext.Material &&
                                                       m.DeletedAt == null)
                       ?? throw new KeyNotFoundException("Không tìm thấy tài liệu để báo cáo.");

            var tutorUserId = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);

            var report = new Report
            {
                Id = Guid.NewGuid().ToString(),
                ReporterId = studentUserId,
                TargetUserId = routeToAdmin ? null : tutorUserId,
                TargetLessonId = lessonId,
                TargetMediaId = media.Id,
                Description = $"[Material:{media.Id}] {reason}",
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.Now
            };

            await _uow.Reports.CreateAsync(report);
            await _uow.SaveChangesAsync();
            return report.Id;
        }

        public async Task<(IReadOnlyList<ReportItemDto> items, int total)> GetForTutorAsync(string tutorUserId, ReportQuery q)
        {
            var (items, total) = await _uow.Reports.GetPagedForTutorAsync(
                tutorUserId, q.Status, q.Keyword, q.Page, q.PageSize);
            return (items.Select(MapItem).ToList(), total);
        }

        public async Task<(IReadOnlyList<ReportItemDto> items, int total)> GetForAdminAsync(ReportQuery q)
        {
            var (items, total) = await _uow.Reports.GetPagedForAdminAsync(
                q.Status, q.Keyword, q.Page, q.PageSize);
            return (items.Select(MapItem).ToList(), total);
        }

        public async Task<ReportDetailDto> GetDetailAsync(string actorUserId, string id, bool isAdmin)
        {
            var r = await _uow.Reports.GetDetailAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy report.");
            if (!isAdmin && r.TargetUserId != actorUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem report này.");
            return MapDetail(r);
        }

        public async Task<bool> UpdateStatusAsync(string actorUserId, string id, ReportStatus status, bool isAdmin)
        {
            var r = await _uow.Reports.GetAsync(x => x.Id == id) ?? throw new KeyNotFoundException("Không tìm thấy report.");
            if (!isAdmin && r.TargetUserId != actorUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền cập nhật report này.");

            r.Status = status;
            await _uow.Reports.UpdateAsync(r);
            await _uow.SaveChangesAsync();
            return true;
        }
    }
}
