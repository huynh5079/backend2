using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IAttendanceRepository : IGenericRepository<Attendance>
    {
        Task<Attendance?> FindAsync(string lessonId, string studentId);
        Task<List<Attendance>> GetByLessonAsync(string lessonId);
        Task<List<Attendance>> GetByStudentRangeAsync(string studentId, DateTime from, DateTime to);
        Task<List<(ScheduleEntry entry, Lesson lesson, Attendance? att)>> GetTutorScheduleAttendancesAsync(string tutorId, DateTime from, DateTime to);
        
        // New methods for moving queries from Service
        Task<Lesson?> GetLessonWithTutorDataAsync(string lessonId);
        Task<List<string>> GetStudentIdsInClassAsync(string classId);
        Task<List<Attendance>> GetAttendancesByLessonIdsAsync(List<string> lessonIds);
        Task<Dictionary<string, string>> GetAttendanceStatusMapAsync(string lessonId, List<string> studentIds);
        Task<List<ClassAssign>> GetClassAssignsWithStudentsAsync(string classId);
    }
}
