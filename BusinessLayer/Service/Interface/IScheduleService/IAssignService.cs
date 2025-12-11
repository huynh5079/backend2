using BusinessLayer.DTOs.Schedule.Class;
using BusinessLayer.DTOs.Schedule.ClassAssign;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface IAssignService
    {
        // Student assign to a recurring class
        Task<ClassAssignDetailDto> AssignRecurringClassAsync(string actorUserId, string userRole, AssignRecurringClassDto dto);

        Task<bool> WithdrawFromClassAsync(string actorUserId, string userRole, string classId, string? studentId);
        
        // New methods for enrollment management
        Task<List<MyEnrolledClassesDto>> GetMyEnrolledClassesAsync(string actorUserId, string userRole, string? studentId);
        Task<EnrollmentCheckDto> CheckEnrollmentAsync(string actorUserId, string userRole, string classId, string? studentId);
        Task<List<StudentEnrollmentDto>> GetStudentsInClassAsync(string tutorUserId, string classId);
        Task<ClassAssignDetailDto> GetEnrollmentDetailAsync(string userId, string classId);

        // Take all students assigned to a tutor's classes
        //Task<List<TutorStudentDto>> GetStudentsByTutorAsync(string tutorUserId);

        // Filter students by tutor and class
        Task<List<RelatedResourceDto>> GetMyTutorsAsync(string actorUserId, string userRole, string? studentId);
        Task<List<RelatedResourceDto>> GetMyStudentsAsync(string tutorUserId);
    }
}
