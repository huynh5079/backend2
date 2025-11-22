using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction.Schedule
{
    public interface ILessonRepository : IGenericRepository<Lesson>
    {
        Task<(Lesson lesson, Class @class)> GetWithClassAsync(string lessonId);
    }
}
