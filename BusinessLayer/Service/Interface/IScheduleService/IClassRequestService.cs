using BusinessLayer.DTOs.Schedule.ClassRequest;
using DataLayer.Entities;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IClassRequestService
    {
        // --- STUDENT's Actions ---
        Task<ClassRequestResponseDto?> CreateClassRequestAsync(string actorUserId, string userRole, CreateClassRequestDto dto);
        Task<ClassRequestResponseDto?> UpdateClassRequestAsync(string actorUserId, string userRole, string requestId, UpdateClassRequestDto dto);
        Task<bool> UpdateClassRequestScheduleAsync(string actorUserId, string userRole, string requestId, List<ClassRequestScheduleDto> scheduleDtos);
        Task<bool> CancelClassRequestAsync(string actorUserId, string userRole, string requestId);
        Task<IEnumerable<ClassRequestResponseDto>> GetMyClassRequestsAsync(string actorUserId, string userRole);

        // --- TUTOR's Actions ---
        Task<IEnumerable<ClassRequestResponseDto>> GetDirectRequestsAsync(string tutorUserId);
        /// <summary>
        /// Respond to direct request. Returns ClassId if accept=true, null if reject.
        /// </summary>
        Task<string?> RespondToDirectRequestAsync(string tutorUserId, string requestId, bool accept, string? meetingLink = null);

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