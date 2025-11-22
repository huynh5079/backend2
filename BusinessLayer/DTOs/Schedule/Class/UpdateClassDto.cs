using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.Class
{
    // DTO only for updating class information
    public class UpdateClassDto
    {
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }


        [StringLength(200, ErrorMessage = "Dịa chỉ là bắt buộc nếu học tại nhà")]
        public string? Location { get; set; }

        [Required]
        [Range(1, 100)]
        public int StudentLimit { get; set; }

        public string? OnlineStudyLink { get; set; }
    }
}
