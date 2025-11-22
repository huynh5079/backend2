using BusinessLayer.Service.Interface;
using BusinessLayer.Storage;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class MediaService : IMediaService
    {
        private readonly IUnitOfWork _uow;
        public MediaService(IUnitOfWork uow) => _uow = uow;

        public async Task<IReadOnlyList<Media>> SaveTutorCertificatesAsync(
            string ownerUserId, string tutorProfileId, IEnumerable<UploadedFileResult> uploads)
        {
            var list = new List<Media>();
            foreach (var up in uploads)
            {
                var m = new Media
                {
                    FileUrl = up.Url,
                    FileName = up.FileName,
                    MediaType = up.ContentType,
                    FileSize = up.FileSize,
                    OwnerUserId = ownerUserId,
                    Context = UploadContext.Certificate,
                    TutorProfileId = tutorProfileId,
                    ProviderPublicId = up.ProviderPublicId
                };
                await _uow.Media.CreateAsync(m);
                list.Add(m);
            }
            await _uow.SaveChangesAsync();
            return list;
        }

        public async Task<IReadOnlyList<Media>> SaveTutorIdentityDocsAsync(
            string ownerUserId, IEnumerable<UploadedFileResult> uploads)
        {
            var list = new List<Media>();
            foreach (var up in uploads)
            {
                var m = new Media
                {
                    FileUrl = up.Url,
                    FileName = up.FileName,
                    MediaType = up.ContentType,
                    FileSize = up.FileSize,
                    OwnerUserId = ownerUserId,
                    Context = UploadContext.IdentityDocument,
                    ProviderPublicId = up.ProviderPublicId
                };
                await _uow.Media.CreateAsync(m);
                list.Add(m);
            }
            await _uow.SaveChangesAsync();
            return list;
        }
    }
}
