using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction.Schedule
{
    public interface IScheduleEntryRepository : IGenericRepository<ScheduleEntry>
    {
        Task<ScheduleEntry?> GetTutorConflictAsync(string tutorProfileId, DateTime startTime, DateTime endTime, string? entryIdToIgnore = null);
    }
}
