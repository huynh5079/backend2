using DataLayer.Repositories.Abstraction.Schedule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IScheduleUnitOfWork : IDisposable
    {
        // repo transaction
        IClassRequestRepository ClassRequests { get; }
        ITutorApplicationRepository TutorApplications { get; }
        IClassAssignRepository ClassAssigns { get; }
        ILessonRepository Lessons { get; }
        IScheduleEntryRepository ScheduleEntries { get; }
        IAvailabilityBlockRepository AvailabilityBlocks { get; }

        // Save all
        Task<int> SaveChangesAsync();
    }
}
