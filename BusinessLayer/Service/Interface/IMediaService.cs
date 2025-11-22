using BusinessLayer.Storage;
using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IMediaService
    {
        Task<IReadOnlyList<Media>> SaveTutorCertificatesAsync(
            string ownerUserId, string tutorProfileId, IEnumerable<UploadedFileResult> uploads);

        Task<IReadOnlyList<Media>> SaveTutorIdentityDocsAsync(
            string ownerUserId, IEnumerable<UploadedFileResult> uploads);
    }
}
