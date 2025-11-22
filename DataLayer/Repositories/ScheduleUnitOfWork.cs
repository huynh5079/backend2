using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class ScheduleUnitOfWork : IScheduleUnitOfWork
    {
        private readonly TpeduContext _ctx;

        // Chỉ chứa các repo MỚI
        public IClassRequestRepository ClassRequests { get; }
        public ITutorApplicationRepository TutorApplications { get; }
        public IClassAssignRepository ClassAssigns { get; }
        public ILessonRepository Lessons { get; }
        public IScheduleEntryRepository ScheduleEntries { get; }
        public IAvailabilityBlockRepository AvailabilityBlocks { get; }

        public ScheduleUnitOfWork(TpeduContext ctx,
            // Inject 6 repo MỚI
            IClassRequestRepository classRequests,
            ITutorApplicationRepository tutorApplications,
            IClassAssignRepository classAssigns,
            ILessonRepository lessons,
            IScheduleEntryRepository scheduleEntries,
            IAvailabilityBlockRepository availabilityBlocks)
        {
            _ctx = ctx;

            // Gán 6 repo MỚI
            ClassRequests = classRequests;
            TutorApplications = tutorApplications;
            ClassAssigns = classAssigns;
            Lessons = lessons;
            ScheduleEntries = scheduleEntries;
            AvailabilityBlocks = availabilityBlocks;
        }

        public Task<int> SaveChangesAsync() => _ctx.SaveChangesAsync();

        public void Dispose() => _ctx.Dispose();
    }
}
