using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.Class
{
    public class UpdateClassScheduleDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "Phải có ít nhất một lịch học lặp lại.")]
        public List<RecurringScheduleRuleDto> ScheduleRules { get; set; } = new();
    }
}
