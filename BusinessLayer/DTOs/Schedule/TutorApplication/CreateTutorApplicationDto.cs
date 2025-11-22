using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.TutorApplication
{
    public class CreateTutorApplicationDto
    {
        [Required(ErrorMessage = "ClassRequestId is required")]
        public string ClassRequestId { get; set; }

        // Chúng ta có thể thêm trường này để gia sư "chào hàng"
        public string? CoverLetter { get; set; }
    }
}
