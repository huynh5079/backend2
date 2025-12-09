using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories
{
    public class QuizQuestionRepository : GenericRepository<QuizQuestion>, IQuizQuestionRepository
    {
        public QuizQuestionRepository(TpeduContext context) : base(context)
        {
        }

        public async Task<QuizQuestion?> GetQuestionWithQuizAndClassAsync(string questionId)
        {
            return await _context.QuizQuestions
                .Include(q => q.Quiz)
                .ThenInclude(qz => qz.Lesson)
                .ThenInclude(l => l.Class)
                .FirstOrDefaultAsync(q => q.Id == questionId && q.DeletedAt == null);
        }
    }
}
