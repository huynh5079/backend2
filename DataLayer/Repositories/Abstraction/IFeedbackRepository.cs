using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IFeedbackRepository : IGenericRepository<Feedback>
    {
        Task<bool> ExistsAsync(string fromUserId, string toUserId, string classId);
        Task<List<Feedback>> GetByClassAsync(string classId);
        Task<(List<Feedback> items, int total)> GetByTutorUserAsync(string tutorUserId, int page, int pageSize);
        Task<(double avg, int count)> CalcTutorRatingAsync(string tutorUserId);
    }
}
