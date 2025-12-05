using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories
{
    public class QuizQuestionRepository : GenericRepository<QuizQuestion>, IQuizQuestionRepository
    {
        public QuizQuestionRepository(TpeduContext context) : base(context)
        {
        }
    }
}
