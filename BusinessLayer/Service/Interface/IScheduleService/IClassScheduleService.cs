using BusinessLayer.DTOs.Schedule.ClassSchedule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IClassScheduleService
    {
        Task<ClassDto> CreateRecurringClassScheduleAsync(string tutorId, CreateClassScheduleDto createDto);
    }
}
