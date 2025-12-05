using BusinessLayer.DTOs.Schedule.Class;
using DataLayer.Entities;
using DataLayer.Enum;
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
        Task<bool> CompleteClassAsync(string tutorUserId, string classId); // Hoàn thành lớp và giải ngân escrow

        // === Tutor (Class cancellation) ===
        Task<CancelClassResponseDto> CancelClassByTutorAsync(string tutorUserId, string classId, string? reason); // Tutor hủy lớp sớm hoặc bỏ giữa chừng

        // === Admin (Class cancellation) ===
        Task<CancelClassResponseDto> CancelClassByAdminAsync(string adminUserId, CancelClassRequestDto request); // Admin hủy lớp
        Task<CancelClassResponseDto> CancelStudentEnrollmentAsync(string adminUserId, string classId, string studentId, ClassCancelReason reason, string? note); // Admin hủy 1 học sinh khỏi lớp
    }
}
