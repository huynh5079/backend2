using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.GenericType.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IMediaRepository : IGenericRepository<Media>
    {
        Task<IReadOnlyList<Media>> GetByOwnerAsync(string ownerUserId);
        Task<IReadOnlyList<Media>> GetByOwnerAndContextAsync(string ownerUserId, UploadContext context);
        Task<IReadOnlyList<Media>> GetCertificatesByTutorProfileAsync(string tutorProfileId);
    }
}
