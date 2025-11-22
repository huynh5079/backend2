using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class MediaRepository : GenericRepository<Media>, IMediaRepository
    {
        public MediaRepository(TpeduContext ctx) : base(ctx) { }

        public async Task<IReadOnlyList<Media>> GetByOwnerAsync(string ownerUserId)
            => await _dbSet.Where(m => m.OwnerUserId == ownerUserId).ToListAsync();

        public async Task<IReadOnlyList<Media>> GetByOwnerAndContextAsync(string ownerUserId, UploadContext context)
            => await _dbSet.Where(m => m.OwnerUserId == ownerUserId && m.Context == context).ToListAsync();

        public async Task<IReadOnlyList<Media>> GetCertificatesByTutorProfileAsync(string tutorProfileId)
            => await _dbSet.Where(m => m.Context == UploadContext.Certificate && m.TutorProfileId == tutorProfileId).ToListAsync();
    }
}
