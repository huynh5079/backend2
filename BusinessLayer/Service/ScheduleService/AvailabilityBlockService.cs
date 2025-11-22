using BusinessLayer.DTOs.Schedule.AvailabilityBlock;
using BusinessLayer.Service.Interface.IScheduleService;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.ScheduleService
{
    public class AvailabilityBlockService : IAvailabilityBlockService
    {
        private readonly IScheduleUnitOfWork _uow;
        private readonly TpeduContext _context;

        public AvailabilityBlockService(
            IScheduleUnitOfWork uow,
            TpeduContext context)
        {
            _uow = uow;
            _context = context;
        }

        public async Task<List<AvailabilityBlockDto>> CreateBlockAsync(string tutorId, CreateAvailabilityBlockDto dto)
        {
            // Logic must be in a transaction with retry
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            var createdBlock = new AvailabilityBlock();

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var newBlock = new AvailabilityBlock
                    {
                        Id = Guid.NewGuid().ToString(),
                        TutorId = tutorId,
                        Title = dto.Title,
                        StartTime = dto.StartTime,
                        EndTime = dto.EndTime,
                        Notes = dto.Notes
                    };

                    // nonsave
                    await _uow.AvailabilityBlocks.CreateAsync(newBlock);
                    createdBlock = newBlock;

                    // check RecurrenceRule
                    if (dto.RecurrenceRule == null)
                    {
                        // if no recurrence rule, throw error
                        throw new InvalidOperationException("RecurrenceRule là bắt buộc khi tạo một block mới.");
                    }

                    // calculate occurrences
                    var occurrences = CalculateOccurrences(
                        dto.StartTime,
                        dto.EndTime,
                        dto.RecurrenceRule
                    );

                    if (!occurrences.Any())
                    {
                        throw new InvalidOperationException("Recurrence rule không tạo ra bất kỳ lịch trống nào.");
                    }

                    // create ScheduleEntry for each occurrence
                    var entriesToCreate = new List<ScheduleEntry>();
                    foreach (var (start, end) in occurrences)
                    {
                        entriesToCreate.Add(new ScheduleEntry
                        {
                            Id = Guid.NewGuid().ToString(),
                            TutorId = tutorId,
                            StartTime = start.ToUniversalTime(), 
                            EndTime = end.ToUniversalTime(),
                            EntryType = EntryType.BLOCK,
                            BlockId = newBlock.Id,
                            LessonId = null
                        });
                    }

                    // add range nonsave
                    await _context.ScheduleEntries.AddRangeAsync(entriesToCreate);

                    // save all changes
                    await _uow.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            // return the created block as a list
            return new List<AvailabilityBlockDto> { MapToDto(createdBlock) };
        }

        public async Task<bool> DeleteBlockAsync(string blockId, string tutorId)
        {
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            var success = false;

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var block = await _uow.AvailabilityBlocks.GetAsync(b => b.Id == blockId && b.TutorId == tutorId);

                    if (block == null)
                    {
                        success = false;
                        await transaction.RollbackAsync();
                        return;
                    }

                    // 2. fetch all ScheduleEntries related to this block
                    var entries = await _uow.ScheduleEntries.GetAllAsync(
                        se => se.BlockId == blockId && se.TutorId == tutorId
                    );

                    // delete entries if any
                    if (entries.Any())
                    {
                        _context.ScheduleEntries.RemoveRange(entries);
                    }

                    // delete the block
                    _context.AvailabilityBlocks.Remove(block);

                    // save all changes
                    await _uow.SaveChangesAsync();
                    await transaction.CommitAsync();
                    success = true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            return success;
        }

        public async Task<IEnumerable<AvailabilityBlockDto>> GetBlocksByTutorAsync(string tutorId, DateTime startDate, DateTime endDate)
        {
            // convert to UTC
            var startUtc = startDate.ToUniversalTime();
            var endUtc = endDate.ToUniversalTime();

            // take all ScheduleEntries of type BLOCK that overlap with the given date range
            var entries = await _uow.ScheduleEntries.GetAllAsync(
                filter: se => se.TutorId == tutorId &&
                              se.EntryType == EntryType.BLOCK &&
                              se.StartTime < endUtc && // query overlap
                              se.EndTime > startUtc,
                includes: query => query.Include(se => se.Block)
            );

            // take distinct blocks
            var blocks = entries
                .Where(se => se.Block != null)
                .Select(se => se.Block!)
                .DistinctBy(b => b.Id);

            return blocks.Select(MapToDto);
        }

        public async Task<AvailabilityBlockDto> UpdateBlockAsync(string blockId, string tutorId, UpdateAvailabilityBlockDto dto)
        {
            var block = await _uow.AvailabilityBlocks.GetAsync(b => b.Id == blockId && b.TutorId == tutorId);

            if (block == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy block với ID '{blockId}' hoặc bạn không có quyền.");
            }

            // update fields
            block.Title = dto.Title;
            block.Notes = dto.Notes;
            block.StartTime = dto.StartTime;
            block.EndTime = dto.EndTime;

            // use nonsave update
            await _uow.AvailabilityBlocks.UpdateAsync(block);

            // save changes
            await _uow.SaveChangesAsync();

            return MapToDto(block);
        }

        private List<(DateTime StartTime, DateTime EndTime)> CalculateOccurrences(
            TimeSpan startTime,
            TimeSpan endTime,
            RecurrenceRuleDto rule)
        {
            var occurrences = new List<(DateTime, DateTime)>();
            // start from today
            var currentDate = DateTime.Today;
            var untilDate = rule.UntilDate.Date;

            // Validate logic for Weekly frequency
            if (!rule.DaysOfWeek.Any() || rule.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
            {
                while (currentDate.Date <= untilDate)
                {
                    if (rule.DaysOfWeek.Contains(currentDate.DayOfWeek))
                    {
                        var occurrenceStart = currentDate.Date.Add(startTime);
                        var occurrenceEnd = currentDate.Date.Add(endTime);
                        occurrences.Add((occurrenceStart, occurrenceEnd));
                    }
                    currentDate = currentDate.AddDays(1);
                }
            }
            // can add more frequency types here in future

            return occurrences;
        }

        // --- Helper Methods ---
        private AvailabilityBlockDto MapToDto(AvailabilityBlock block)
        {
            return new AvailabilityBlockDto
            {
                Id = block.Id,
                TutorId = block.TutorId,
                Title = block.Title,
                StartTime = block.StartTime, // Keep as UTC or convert to local as needed for API
                EndTime = block.EndTime,   // Keep as UTC or convert to local as needed for API
                Notes = block.Notes,
                CreatedAt = block.CreatedAt,
                UpdatedAt = block.UpdatedAt
            };
        }
    }
}
