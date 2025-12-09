using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;

namespace DataLayer.Repositories.Abstraction
{
    public interface IVideoAnalysisRepository : IGenericRepository<VideoAnalysis>
    {
        Task<VideoAnalysis?> GetByMediaIdAsync(string mediaId);
        Task<IReadOnlyList<VideoAnalysis>> GetByLessonIdAsync(string lessonId);
    }
}

