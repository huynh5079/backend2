using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;

namespace DataLayer.Repositories
{
    public class StudentQuizAttemptRepository : GenericRepository<StudentQuizAttempt>, IStudentQuizAttemptRepository
    {
        public StudentQuizAttemptRepository(TpeduContext context) : base(context)
        {
        }
    }
}
