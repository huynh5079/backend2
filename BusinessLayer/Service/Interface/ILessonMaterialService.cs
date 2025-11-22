using BusinessLayer.DTOs.LessonMaterials;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface ILessonMaterialService
    {
        Task<IReadOnlyList<MaterialItemDto>> ListAsync(string actorUserId, string lessonId);
        Task<IReadOnlyList<MaterialItemDto>> UploadAsync(string tutorUserId, string lessonId, IEnumerable<IFormFile> files, CancellationToken ct);
        Task<IReadOnlyList<MaterialItemDto>> AddLinksAsync(string tutorUserId, string lessonId, IEnumerable<(string url, string? title)> links);
        Task<bool> DeleteAsync(string tutorUserId, string lessonId, string mediaId, CancellationToken ct);
    }
}
