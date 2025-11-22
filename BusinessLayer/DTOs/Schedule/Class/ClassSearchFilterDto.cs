using DataLayer.Enum;

namespace BusinessLayer.DTOs.Schedule.Class
{
    public class ClassSearchFilterDto
    {
        // Search
        public string? Keyword { get; set; } // Tìm theo tên lớp, môn học, mô tả, tên gia sư...

        // Filter
        public string? Subject { get; set; } // Môn học
        public string? EducationLevel { get; set; } // Khối/lớp
        public ClassMode? Mode { get; set; } // Online/Offline
        public string? Area { get; set; } // Khu vực (từ Address của Tutor)
        public decimal? MinPrice { get; set; } // Học phí tối thiểu
        public decimal? MaxPrice { get; set; } // Học phí tối đa
        public ClassStatus? Status { get; set; } // Trạng thái lớp (Active, Ongoing...)

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

