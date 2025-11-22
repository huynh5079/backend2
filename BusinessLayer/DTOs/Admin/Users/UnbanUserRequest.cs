using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.Users
{
    public class UnbanUserRequest
    {
        public string? Reason { get; set; } // optional: lý do mở khóa
    }
}
