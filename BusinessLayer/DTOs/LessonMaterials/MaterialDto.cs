using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.LessonMaterials
{
    public class MaterialItemDto
    {
        public string Id { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public string Url { get; set; } = default!;
        public string MediaType { get; set; } = default!;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UploadedByUserId { get; set; } = default!;
    }

    // Upload nhiều file
    public class UploadLessonMaterialsRequest
    {
        public required List<IFormFile> Files { get; set; }
    }

    // Thêm 1..n link (YouTube/Drive/URL bất kỳ)
    public class AddLessonLinksRequest
    {
        public required List<string> Links { get; set; }
        // optional: tiêu đề hiển thị
        public List<string>? Titles { get; set; }
    }
}