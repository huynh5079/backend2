using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories
{
    public class StudentQuizAnswerRepository : GenericRepository<StudentQuizAnswer>, IStudentQuizAnswerRepository
    {
        public StudentQuizAnswerRepository(TpeduContext context) : base(context)
        {
        }
    }
}
