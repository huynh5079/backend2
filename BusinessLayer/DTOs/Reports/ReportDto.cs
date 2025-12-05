using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Reports
{
    public class ReportCreateRequest
    {
        public required string Reason { get; set; }
    }

    public class ReportItemDto
    {
        public string Id { get; set; } = default!;
        public string? ReporterId { get; set; }
        public string? TargetUserId { get; set; }   // null = Admin
        public string? TargetLessonId { get; set; }
        public string? TargetMediaId { get; set; }
        public string? Description { get; set; }
        public ReportStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReportDetailDto : ReportItemDto
    {
        public string? ReporterEmail { get; set; }
        public string? TargetUserEmail { get; set; }
        public string? LessonTitle { get; set; }
        public string? MediaFileName { get; set; }
    }

    public class ReportUpdateStatusRequest
    {
        public required ReportStatus Status { get; set; }
        public string? Note { get; set; } // nếu muốn log nội bộ
    }

    // filter/paging cho list
    public class ReportQuery
    {
        public ReportStatus? Status { get; set; }
        public string? Keyword { get; set; }  // tìm trong Description
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
