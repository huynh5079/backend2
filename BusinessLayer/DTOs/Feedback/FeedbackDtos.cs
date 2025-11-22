using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Feedback
{
    public class CreateFeedbackRequest
    {
        public string ClassId { get; set; } = default!;
        public string ToUserId { get; set; } = default!;  // người nhận (VD: tutor hoặc student)
        public float? Rating { get; set; }                // 0..5 (nullable, cho phép chỉ comment)
        public string? Comment { get; set; }              // <= 1000 ký tự
    }

    public class CreateTutorProfileFeedbackRequest
    {
        // public string TutorUserId { get; set; } = default!;
        public string? Comment { get; set; }          // hiển thị công khai trên trang Tutor
        public float? Rating { get; set; }            // được phép gửi nhưng SẼ KHÔNG tính vào rating tổng
    }

    public class UpdateFeedbackRequest
    {
        public float? Rating { get; set; }     // optional
        public string? Comment { get; set; }   // optional
    }

    public class FeedbackDto
    {
        public string Id { get; set; } = default!;

        public string ClassId { get; set; } = default!;
        public string FromUserId { get; set; } = default!;
        public string FromUserName { get; set; } = default!;
        public string ToUserId { get; set; } = default!;
        public string ToUserName { get; set; } = default!;

        public float? Rating { get; set; }
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class TutorRatingSummaryDto
    {
        public string TutorUserId { get; set; } = default!;
        public double Average { get; set; }
        public int Count { get; set; }
    }
}
