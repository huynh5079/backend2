using BusinessLayer.DTOs.Schedule.AvailabilityBlock;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IAvailabilityBlockService
    {
        Task<List<AvailabilityBlockDto>> CreateBlockAsync(string tutorId, CreateAvailabilityBlockDto dto);

        Task<IEnumerable<AvailabilityBlockDto>> GetBlocksByTutorAsync(string tutorId, DateTime startDate, DateTime endDate);

        Task<AvailabilityBlockDto> UpdateBlockAsync(string blockId, string tutorId, UpdateAvailabilityBlockDto dto);

        Task<bool> DeleteBlockAsync(string blockId, string tutorId);

    }
}