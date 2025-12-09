using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;

namespace DataLayer.Repositories
{
    public class VideoAnalysisRepository : GenericRepository<VideoAnalysis>, IVideoAnalysisRepository
    {
        public VideoAnalysisRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<VideoAnalysis?> GetByMediaIdAsync(string mediaId)
            => await _dbSet
                .Include(v => v.Media)
                .Include(v => v.Lesson)
                .FirstOrDefaultAsync(v => v.MediaId == mediaId);

        public async Task<IReadOnlyList<VideoAnalysis>> GetByLessonIdAsync(string lessonId)
            => await _dbSet
                .Include(v => v.Media)
                .Where(v => v.LessonId == lessonId)
                .ToListAsync();
    }
}

