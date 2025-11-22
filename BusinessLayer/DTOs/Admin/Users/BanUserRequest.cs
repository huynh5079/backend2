using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.Users
{
    public class BanUserRequest
    {
        public string? Reason { get; set; }
        public int? DurationDays { get; set; } // optional: ban n ngày
    }
}