using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction
{
    public interface IQuizRepository : IGenericRepository<Quiz>
    {
        Task<Quiz?> GetQuizWithQuestionsAsync(string quizId);
        Task<Quiz?> GetQuizWithDetailsAsync(string quizId);
        Task<IEnumerable<Quiz>> GetQuizzesByLessonIdAsync(string lessonId);
    }
}
