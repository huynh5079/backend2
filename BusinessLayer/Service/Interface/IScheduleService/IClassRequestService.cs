using BusinessLayer.DTOs.Schedule.ClassRequest;
using DataLayer.Entities;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IClassRequestService
    {
        // --- STUDENT's Actions ---
        Task<ClassRequestResponseDto?> CreateClassRequestAsync(string studentUserId, CreateClassRequestDto dto);
        Task<ClassRequestResponseDto?> UpdateClassRequestAsync(string studentUserId, string requestId, UpdateClassRequestDto dto);
        Task<bool> UpdateClassRequestScheduleAsync(string studentUserId, string requestId, List<ClassRequestScheduleDto> scheduleDtos);
        Task<bool> CancelClassRequestAsync(string studentUserId, string requestId);
        Task<IEnumerable<ClassRequestResponseDto>> GetMyClassRequestsAsync(string studentUserId);

        // --- TUTOR's Actions ---
        Task<IEnumerable<ClassRequestResponseDto>> GetDirectRequestsAsync(string tutorUserId);
        Task<bool> RespondToDirectRequestAsync(string tutorUserId, string requestId, bool accept);

        // --- PUBLIC/SHARED Actions ---
        Task<ClassRequestResponseDto?> GetClassRequestByIdAsync(string id);
        Task<(IEnumerable<ClassRequestResponseDto> Data, int TotalCount)> GetMarketplaceRequestsAsync(
            int page, 
            int pageSize, 
            string? status = null, 
            string? subject = null,
            string? educationLevel = null, 
            string? mode = null, 
            string? locationContains = null); 

        // --- ADMIN/SYSTEM Actions ---
        Task<bool> UpdateClassRequestStatusAsync(string id, UpdateStatusDto dto);
        Task<int> ExpireClassRequestsAsync();
        Task<bool> DeleteClassRequestAsync(string id);
    }
}