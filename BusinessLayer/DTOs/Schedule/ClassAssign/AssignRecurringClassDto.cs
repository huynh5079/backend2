using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Schedule.ClassAssign
{
    public class AssignRecurringClassDto
    {
        [Required(ErrorMessage = "Class ID là bắt buộc.")]
        public string ClassId { get; set; } = null!;
    }
}