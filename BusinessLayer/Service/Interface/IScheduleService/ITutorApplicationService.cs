using BusinessLayer.DTOs.Schedule.TutorApplication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface ITutorApplicationService
    {
        // --- TUTOR's Actions ---
        Task<TutorApplicationResponseDto?> CreateApplicationAsync(string tutorUserId, CreateTutorApplicationDto dto);
        Task<bool> WithdrawApplicationAsync(string tutorUserId, string applicationId);
        Task<IEnumerable<TutorApplicationResponseDto>> GetMyApplicationsAsync(string tutorUserId);

        // --- STUDENT's Actions ---
        Task<IEnumerable<TutorApplicationResponseDto>> GetApplicationsForMyRequestAsync(string studentUserId, string classRequestId);
        Task<bool> AcceptApplicationAsync(string studentUserId, string applicationId);
        Task<bool> RejectApplicationAsync(string studentUserId, string applicationId);
    }
}
