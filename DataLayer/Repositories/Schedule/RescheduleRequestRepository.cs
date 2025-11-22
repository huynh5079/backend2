using DataLayer.Entities;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Schedule
{
    public class RescheduleRequestRepository : GenericRepository<RescheduleRequest>, IRescheduleRequestRepository
    {
        public RescheduleRequestRepository(TpeduContext context) : base(context)
        {
        }
    }
}
