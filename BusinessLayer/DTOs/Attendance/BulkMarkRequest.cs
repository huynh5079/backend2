using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Attendance
{
    public class BulkMarkRequest
    {
        public Dictionary<string, string> StudentStatus { get; set; } = new(); // studentId -> "Present"/"Late"/...
        public string? Notes { get; set; }
    }
}
