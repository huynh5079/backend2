using BusinessLayer.DTOs.Schedule.RescheduleRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface ILessonRescheduleService
    {
        /// <summary>
        /// (Tutor) Tạo yêu cầu đổi lịch cho một buổi học
        /// </summary>
        Task<RescheduleRequestDto> CreateRequestAsync(string tutorUserId, string lessonId, CreateRescheduleRequestDto dto);

        /// <summary>
        /// (Student/Parent) Tạo yêu cầu đổi lịch cho một buổi học
        /// </summary>
        Task<RescheduleRequestDto> CreateRequestByStudentAsync(string actorUserId, string lessonId, CreateRescheduleRequestDto dto);

        /// <summary>
        /// (Student/Parent hoặc Tutor) Chấp nhận yêu cầu đổi lịch
        /// </summary>
        Task<RescheduleRequestDto> AcceptRequestAsync(string actorUserId, string requestId);

        /// <summary>
        /// (Student/Parent hoặc Tutor) Từ chối yêu cầu đổi lịch
        /// </summary>
        Task<RescheduleRequestDto> RejectRequestAsync(string actorUserId, string requestId);

        /// <summary>
        /// Lấy danh sách các yêu cầu đang chờ (cho cả 2 bên)
        /// </summary>
        Task<IEnumerable<RescheduleRequestDto>> GetPendingRequestsAsync(string actorUserId);
    }
}
