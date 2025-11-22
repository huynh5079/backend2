using DataLayer.Enum;

namespace BusinessLayer.DTOs.Admin.Tutors
{
    public class TutorSearchFilterDto
    {
        // Search
        public string? Keyword { get; set; } // Tìm theo tên gia sư, môn học, mô tả...

        // Filter
        public string? Subject { get; set; } // Môn học (Toán, Lý, Hóa...)
        public string? EducationLevel { get; set; } // Khối/lớp (Lớp 10, Lớp 11, THPT...)
        public ClassMode? Mode { get; set; } // Online/Offline
        public string? Area { get; set; } // Khu vực (từ Address của User)
        public Gender? Gender { get; set; } // Giới tính gia sư
        public double? MinRating { get; set; } // Rating tối thiểu
        public decimal? MinPrice { get; set; } // Học phí tối thiểu (từ Class)
        public decimal? MaxPrice { get; set; } // Học phí tối đa (từ Class)

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 6;
    }
}

