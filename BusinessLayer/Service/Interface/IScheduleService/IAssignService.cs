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
        Task<ClassDto> AssignRecurringClassAsync(string studentUserId, string classId);

        Task<bool> WithdrawFromClassAsync(string studentUserId, string classId);
        
        // New methods for enrollment management
        Task<List<MyEnrolledClassesDto>> GetMyEnrolledClassesAsync(string studentUserId);
        Task<EnrollmentCheckDto> CheckEnrollmentAsync(string studentUserId, string classId);
        Task<List<StudentEnrollmentDto>> GetStudentsInClassAsync(string tutorUserId, string classId);
        Task<ClassAssignDetailDto> GetEnrollmentDetailAsync(string userId, string classId);

        // Take all students assigned to a tutor's classes
        Task<List<TutorStudentDto>> GetStudentsByTutorAsync(string tutorUserId);
    }
}
