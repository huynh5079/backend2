using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories
{
    public class QuizRepository : GenericRepository<Quiz>, IQuizRepository
    {
        public QuizRepository(TpeduContext context) : base(context)
        {
        }

        public async Task<Quiz?> GetQuizWithQuestionsAsync(string quizId)
        {
            return await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Lesson)
                .ThenInclude(l => l.Class)
                .FirstOrDefaultAsync(q => q.Id == quizId && q.DeletedAt == null);
        }

        public async Task<Quiz?> GetQuizWithDetailsAsync(string quizId)
        {
            return await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Lesson)
                .ThenInclude(l => l.Class)
                .FirstOrDefaultAsync(q => q.Id == quizId && q.DeletedAt == null && q.IsActive);
        }

        public async Task<IEnumerable<Quiz>> GetQuizzesByLessonIdAsync(string lessonId)
        {
            return await _context.Quizzes
                .Include(q => q.Questions)
                .Where(q => q.LessonId == lessonId && q.DeletedAt == null)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }
    }
}
