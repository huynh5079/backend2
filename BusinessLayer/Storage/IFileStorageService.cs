using DataLayer.Enum;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Storage
{
    public interface IFileStorageService
    {
        Task<IReadOnlyList<UploadedFileResult>> UploadManyAsync(
            IEnumerable<IFormFile> files,
            UploadContext context,
            string ownerUserId,
            CancellationToken ct = default
        );
    }
}
