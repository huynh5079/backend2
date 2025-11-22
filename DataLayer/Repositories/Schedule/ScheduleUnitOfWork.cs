using DataLayer.Entities;
using DataLayer.Repositories.Abstraction.Schedule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Schedule
{
    public class ScheduleUnitOfWork : IScheduleUnitOfWork
    {
        private readonly TpeduContext _ctx;

        // 
        public IClassRequestRepository ClassRequests { get; }
        public ITutorApplicationRepository TutorApplications { get; }
        public IClassAssignRepository ClassAssigns { get; }
        public ILessonRepository Lessons { get; }
        public IScheduleEntryRepository ScheduleEntries { get; }
        public IAvailabilityBlockRepository AvailabilityBlocks { get; }
        public IClassRepository2 Classes { get; }

        public ScheduleUnitOfWork(TpeduContext ctx,
            // 
            IClassRequestRepository classRequests,
            ITutorApplicationRepository tutorApplications,
            IClassAssignRepository classAssigns,
            ILessonRepository lessons,
            IScheduleEntryRepository scheduleEntries,
            IAvailabilityBlockRepository availabilityBlocks,
            IClassRepository2 classes)
        {
            _ctx = ctx;

            // 
            ClassRequests = classRequests;
            TutorApplications = tutorApplications;
            ClassAssigns = classAssigns;
            Lessons = lessons;
            ScheduleEntries = scheduleEntries;
            AvailabilityBlocks = availabilityBlocks;
            Classes = classes;
        }

        public Task<int> SaveChangesAsync() => _ctx.SaveChangesAsync();
        public void Dispose() => _ctx.Dispose();
    }

}
