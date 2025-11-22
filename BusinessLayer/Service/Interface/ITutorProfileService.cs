using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface ITutorProfileService
    {
        Task<string?> GetTutorProfileIdByUserIdAsync(string userId);
    }
}
