using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class ScheduleGenerationService : IScheduleGenerationService
    {
        private readonly TpeduContext _context;
        private class StandardScheduleRule
        {
            public DayOfWeek DayOfWeek { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
        }

        public ScheduleGenerationService(TpeduContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Implement function from Interface (ClassRequest Stream)
        /// (Entity C# uses TimeOnly)
        /// </summary>
        public async Task GenerateScheduleFromRequestAsync(string classId, string tutorId, DateTime startDate, IEnumerable<ClassRequestSchedule> scheduleRules)
        { 
            var standardRules = scheduleRules.Select(r => new StandardScheduleRule
            {
                DayOfWeek = (DayOfWeek)r.DayOfWeek,
                StartTime = r.StartTime, 
                EndTime = r.EndTime 
            });
            // Core logic function
            await GenerateScheduleLogicAsync(classId, tutorId, startDate, standardRules);
        }

        /// <summary>
        /// Implement function from Interface (Class stream created by Tutor)
        /// (Entity C# uses TimeOnly)
        /// </summary>
        public async Task GenerateScheduleFromClassAsync(string classId, string tutorId, DateTime startDate, IEnumerable<ClassSchedule> scheduleRules)
        {
            var standardRules = scheduleRules.Select(r => new StandardScheduleRule
            {
                DayOfWeek = (DayOfWeek)r.DayOfWeek,

                StartTime = r.StartTime, 
                EndTime = r.EndTime 
            });

            // Core logic function
            await GenerateScheduleLogicAsync(classId, tutorId, startDate, standardRules);
        }

        /// <summary>
        /// Core logic: Find empty slot and AddRange to Context
        /// </summary>
        private async Task GenerateScheduleLogicAsync(string classId, string tutorId, DateTime startDate, IEnumerable<StandardScheduleRule> rules)
        {
            int totalSlotsToFind = rules.Count() * 4;
            int searchDayLimit = 100; // Tăng từ 50 lên 100 ngày để tìm đủ slot

            var foundSlots = await FindAvailableSlotsAsync(
                tutorId, startDate, rules, totalSlotsToFind, searchDayLimit
            );

            var newLessons = new List<Lesson>();
            var newScheduleEntries = new List<ScheduleEntry>();

            foreach (var slot in foundSlots)
            {
                var newLesson = new Lesson
                {
                    Id = Guid.NewGuid().ToString(),
                    ClassId = classId,
                    Status = LessonStatus.SCHEDULED,
                    Title = $"Buổi học ngày {slot.StartTimeUtc.ToLocalTime().ToString("dd/MM/yyyy")}"
                };
                newLessons.Add(newLesson);

                var newEntry = new ScheduleEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    TutorId = tutorId,
                    StartTime = slot.StartTimeUtc,
                    EndTime = slot.EndTimeUtc,
                    EntryType = EntryType.LESSON,
                    LessonId = newLesson.Id,
                    BlockId = null
                };
                newScheduleEntries.Add(newEntry);
            }

            // AddRange to Context (no SaveChanges)
            await _context.Lessons.AddRangeAsync(newLessons);
            await _context.ScheduleEntries.AddRangeAsync(newScheduleEntries);
        }

        /// <summary>
        /// Helper finds empty slots    
        /// /// </summary>
        private async Task<List<(DateTime StartTimeUtc, DateTime EndTimeUtc)>> FindAvailableSlotsAsync(
            string tutorId,
            DateTime searchStartDate,
            IEnumerable<StandardScheduleRule> rules,
            int totalSlotsToFind,
            int searchDayLimit)
        {
            var foundSlots = new List<(DateTime StartTimeUtc, DateTime EndTimeUtc)>();
            var currentDate = searchStartDate.Date;
            var endDateLimit = currentDate.AddDays(searchDayLimit);

            var rulesDict = rules.GroupBy(r => r.DayOfWeek)
                                 .ToDictionary(g => g.Key, g => g.Select(r => (r.StartTime, r.EndTime)).ToList());

            var existingEntries = await _context.ScheduleEntries
                .Where(se => se.TutorId == tutorId &&
                             se.DeletedAt == null &&
                             se.StartTime < endDateLimit &&
                             se.EndTime > searchStartDate)
                .Select(se => new { se.StartTime, se.EndTime })
                .ToListAsync();

            while (foundSlots.Count < totalSlotsToFind && currentDate < endDateLimit)
            {
                if (rulesDict.TryGetValue(currentDate.DayOfWeek, out var timesInDay))
                {
                    foreach (var timeRule in timesInDay)
                    {
                        var potentialStartTime = currentDate.Add(timeRule.StartTime).ToUniversalTime();
                        var potentialEndTime = currentDate.Add(timeRule.EndTime).ToUniversalTime();

                        if (potentialStartTime >= searchStartDate.ToUniversalTime())
                        {
                            bool isConflict = existingEntries.Any(existing =>
                                potentialStartTime < existing.EndTime &&
                                potentialEndTime > existing.StartTime
                            );

                            if (!isConflict)
                            {
                                foundSlots.Add((potentialStartTime, potentialEndTime));
                                existingEntries.Add(new { StartTime = potentialStartTime, EndTime = potentialEndTime });
                                if (foundSlots.Count >= totalSlotsToFind) break;
                            }
                        }
                    }
                }

                if (foundSlots.Count >= totalSlotsToFind)
                    break;

                currentDate = currentDate.AddDays(1);
            }

            if (foundSlots.Count < totalSlotsToFind)
            {
                throw new InvalidOperationException($"Không thể tìm đủ {totalSlotsToFind} buổi học trống cho gia sư trong vòng {searchDayLimit} ngày tới. Chỉ tìm thấy {foundSlots.Count} buổi.");
            }

            return foundSlots;
        }
    }
}
