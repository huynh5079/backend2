using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Admin.Tutors
{
    public class TutorActionRequests
    {
        public class RejectTutorRequest
        {
            public string? RejectReason { get; set; }
        }

        public class ProvideTutorRequest
        {
            public string? ProvideText { get; set; }
        }
    }
}
