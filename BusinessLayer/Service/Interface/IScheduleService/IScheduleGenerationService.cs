using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IScheduleGenerationService
    {
        /// <summary>/// Create a schedule (Lesson + ScheduleEntry) from an accepted ClassRequest.
        /// /// Logic: Repeat the lessons x4 (4 weeks) within the next 50 days.
        /// /// </summary>
        Task GenerateScheduleFromRequestAsync(
            string classId,
            string tutorId,
            DateTime startDate,
            IEnumerable<ClassRequestSchedule> scheduleRules);

        /// <summary>/// Create a schedule (Lesson + ScheduleEntry) from an accepted ClassRequest.
        /// /// Logic: Repeat the lessons x4 (4 weeks) within the next 50 days.
        /// /// </summary>
        Task GenerateScheduleFromClassAsync(
            string classId,
            string tutorId,
            DateTime startDate,
            IEnumerable<ClassSchedule> scheduleRules);
    }
}

