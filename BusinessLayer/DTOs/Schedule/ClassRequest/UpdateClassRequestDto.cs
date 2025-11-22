using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Schedule.ClassRequest
{
    public class UpdateClassRequestDto
    {
        [StringLength(1000, ErrorMessage = "Bạn cần nhập mô tả")]
        public string? Description { get; set; }

        [StringLength(200, ErrorMessage = "Dịa chỉ là bắt buộc nếu học tại nhà")]
        public string? Location { get; set; }

        [StringLength(500, ErrorMessage = "Bạn cần viết ra yêu cầu của mình")]
        public string? SpecialRequirements { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Ngân sách phải lớn hơn 0")]
        public decimal? Budget { get; set; }

        [StringLength(200, ErrorMessage = "Hình thức học (Online/Ofline) là bắt buộc")]
        public ClassRequestStatus? Status { get; set; }
        public string? OnlineStudyLink { get; set; }
        public ClassMode? Mode { get; set; }

        [StringLength(500, ErrorMessage = "Ngày học dự kiến không thể quá gần hoặc ở quá khứ")]
        public DateTime? ClassStartDate { get; set; }
    }
}