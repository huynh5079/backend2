using BusinessLayer.DTOs.Schedule.Lesson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface.IScheduleService
{
    public interface ILessonService
    {
        Task<IEnumerable<ClassLessonDto>> GetLessonsByClassIdAsync(string classId);
        Task<LessonDetailDto?> GetLessonDetailAsync(string lessonId);
        Task<TutorLessonDetailDto?> GetTutorLessonDetailAsync(string lessonId, string tutorUserId);
    }
}
