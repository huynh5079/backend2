using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Entities
{
    public partial class RescheduleRequest : BaseEntity
    {
        // Ai là người yêu cầu (Luôn là Tutor)
        [ForeignKey(nameof(RequesterUser))]
        public string RequesterUserId { get; set; } = default!;

        // Buổi học nào cần đổi
        [ForeignKey(nameof(Lesson))]
        public string LessonId { get; set; } = default!;

        // Lịch học (ScheduleEntry) nào bị ảnh hưởng
        [ForeignKey(nameof(OriginalScheduleEntry))]
        public string OriginalScheduleEntryId { get; set; } = default!;

        // Thông tin lịch cũ (để hiển thị)
        public DateTime OldStartTime { get; set; }
        public DateTime OldEndTime { get; set; }

        // Thông tin lịch mới (đề xuất)
        public DateTime NewStartTime { get; set; }
        public DateTime NewEndTime { get; set; }

        public string? Reason { get; set; }
        public RescheduleStatus Status { get; set; } = RescheduleStatus.Pending;

        // Người phản hồi (Student/Parent)
        [ForeignKey(nameof(ResponderUser))]
        public string? ResponderUserId { get; set; }
        public DateTime? RespondedAt { get; set; }

        // Navigation properties
        public virtual User RequesterUser { get; set; } = default!;
        public virtual User? ResponderUser { get; set; }
        public virtual Lesson Lesson { get; set; } = default!;
        public virtual ScheduleEntry OriginalScheduleEntry { get; set; } = default!;
    }
}
