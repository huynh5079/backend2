using BusinessLayer.DTOs.Schedule.Class;
using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IClassService
    {
        // === Public (Student/Guest) ===
        Task<ClassDto?> GetClassByIdAsync(string classId);
        Task<IEnumerable<ClassDto>> GetAvailableClassesAsync(); // Lấy các lớp Pending/Active
        Task<PaginationResult<ClassDto>> SearchAndFilterAvailableAsync(ClassSearchFilterDto filter);

        // === Tutor (Class management) ===
        Task<ClassDto> CreateRecurringClassScheduleAsync(string tutorId, CreateClassDto createDto);
        Task<IEnumerable<ClassDto>> GetMyClassesAsync(string tutorUserId);
        Task<ClassDto?> UpdateClassAsync(string tutorUserId, string classId, UpdateClassDto dto);
        Task<bool> UpdateClassScheduleAsync(string tutorUserId, string classId, UpdateClassScheduleDto dto);
        Task<bool> DeleteClassAsync(string tutorUserId, string classId);
    }
}
