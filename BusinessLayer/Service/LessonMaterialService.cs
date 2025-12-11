using BusinessLayer.DTOs.LessonMaterials;
using BusinessLayer.Service.Interface;
using BusinessLayer.Storage;
using System.Threading;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class LessonMaterialService : ILessonMaterialService
    {
        private readonly IUnitOfWork _uow;
        private readonly IFileStorageService _storage;
        private readonly IVideoAnalysisService? _videoAnalysisService;
        private readonly IMaterialContentValidatorService _materialValidator;

        public LessonMaterialService(
            IUnitOfWork uow, 
            IFileStorageService storage,
            IMaterialContentValidatorService materialValidator,
            IVideoAnalysisService? videoAnalysisService = null)
        {
            _uow = uow;
            _storage = storage;
            _materialValidator = materialValidator;
            _videoAnalysisService = videoAnalysisService;
        }

        private static MaterialItemDto Map(Media m) => new()
        {
            Id = m.Id,
            FileName = m.FileName,
            Url = m.FileUrl,
            MediaType = m.MediaType,
            FileSize = m.FileSize,
            CreatedAt = m.CreatedAt,
            UploadedByUserId = m.OwnerUserId
        };

        // ===== List (Tutor/Student/Parent) =====
        public async Task<IReadOnlyList<MaterialItemDto>> ListAsync(string actorUserId, string lessonId)
        {
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);

            if (lesson == null || cls == null)
                throw new UnauthorizedAccessException("Không tìm thấy bài học hoặc lớp học.");

            // Quyền xem: Tutor chủ lớp hoặc Student/Parent có ClassAssign approved trong lớp
            var tutorUserId = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
            var isTutorOwner = tutorUserId == actorUserId;

            var isAllowed = isTutorOwner; // Bắt đầu bằng quyền Tutor

            if (!isAllowed)
            {
                // Kiểm tra Student: phải có ClassAssign approved trong lớp này
                var studentProfileId = await _uow.StudentProfiles.GetIdByUserIdAsync(actorUserId);
                if (studentProfileId != null)
                {
                    isAllowed = await _uow.ClassAssigns.IsApprovedAsync(cls.Id, studentProfileId);
                    if (!isAllowed)
                    {
                        throw new UnauthorizedAccessException("Bạn chưa tham gia lớp học này. Vui lòng đăng ký và được phê duyệt để xem tài liệu.");
                    }
                }
            }

            if (!isAllowed)
            {
                // Kiểm tra Parent: ít nhất một con phải có ClassAssign approved trong lớp này
                var parentProfile = await _uow.ParentProfiles.GetByUserIdAsync(actorUserId);
                if (parentProfile != null)
                {
                    var childrenIds = await _uow.ParentProfiles.GetChildrenIdsAsync(actorUserId);
                    if (childrenIds == null || !childrenIds.Any())
                    {
                        throw new UnauthorizedAccessException("Bạn chưa liên kết với học sinh nào.");
                    }
                    
                    isAllowed = await _uow.ClassAssigns.IsAnyChildApprovedAsync(cls.Id, childrenIds);
                    if (!isAllowed)
                    {
                        throw new UnauthorizedAccessException("Con của bạn chưa tham gia lớp học này. Vui lòng đăng ký và được phê duyệt để xem tài liệu.");
                    }
                }
            }

            // Kiểm tra lần cuối: nếu không phải Tutor, không phải Student, không phải Parent
            if (!isAllowed)
            {
                throw new UnauthorizedAccessException("Chỉ gia sư, học sinh và phụ huynh mới có quyền xem tài liệu buổi học này.");
            }

            var items = await _uow.Media.GetAllAsync(
                filter: m => m.LessonId == lessonId
                          && m.Context == UploadContext.Material
                          && m.DeletedAt == null,
                includes: q => q.OrderByDescending(m => m.CreatedAt)
            );

            return items.Select(Map).ToList();
        }

        // ===== Upload files (Tutor) =====
        public async Task<IReadOnlyList<MaterialItemDto>> UploadAsync(
            string tutorUserId, string lessonId, IEnumerable<IFormFile> files, CancellationToken ct)
        {
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);
            var tutorUserIdOfClass = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
            if (tutorUserIdOfClass != tutorUserId)
                throw new UnauthorizedAccessException("Chỉ gia sư của lớp mới được upload tài liệu.");

            // Validate all files before uploading
            var fileList = files.ToList();
            var subject = cls.Subject ?? "Unknown";
            var educationLevel = cls.EducationLevel ?? "Unknown";

            // Check file size limits (Cloudinary and practical limits)
            const long maxFileSize = 100_000_000; // 100 MB limit
            foreach (var file in fileList)
            {
                if (file.Length > maxFileSize)
                {
                    throw new InvalidOperationException(
                        $"File '{file.FileName}' quá lớn ({file.Length / 1_000_000} MB). " +
                        $"Kích thước tối đa cho phép là {maxFileSize / 1_000_000} MB.");
                }
            }

            // Validate content
            foreach (var file in fileList)
            {
                var validationResult = await _materialValidator.ValidateFileAsync(file, subject, educationLevel, ct);
                    
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException(
                        $"File '{file.FileName}' vi phạm quy định: {validationResult.ErrorMessage}");
                }
            }

            var ups = await _storage.UploadManyAsync(fileList, UploadContext.Material, tutorUserId, ct);

            var list = new List<Media>();
            foreach (var up in ups)
            {
                var m = new Media
                {
                    FileUrl = up.Url,
                    FileName = up.FileName,
                    MediaType = up.ContentType,
                    FileSize = up.FileSize,
                    OwnerUserId = tutorUserId,
                    Context = UploadContext.Material,
                    LessonId = lessonId,
                    ProviderPublicId = up.ProviderPublicId
                };
                await _uow.Media.CreateAsync(m);
                list.Add(m);

                // Tự động trigger phân tích video nếu là file video
                if (_videoAnalysisService != null && IsVideoFile(up.ContentType))
                {
                    // Chạy trong background để không block response
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _videoAnalysisService.AnalyzeVideoAsync(m.Id, lessonId, m.FileUrl, ct);
                        }
                        catch (Exception ex)
                        {
                            // Log error nhưng không throw (để không ảnh hưởng upload)
                            // Có thể log vào hệ thống logging sau
                            Console.WriteLine($"Error analyzing video {m.Id}: {ex.Message}");
                        }
                    }, ct);
                }
            }
            await _uow.SaveChangesAsync();
            return list.Select(Map).ToList();
        }

        // ===== Add links (Tutor) =====
        public async Task<IReadOnlyList<MaterialItemDto>> AddLinksAsync(
            string tutorUserId, string lessonId, IEnumerable<(string url, string? title)> links)
        {
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);
            var tutorUserIdOfClass = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
            if (tutorUserIdOfClass != tutorUserId)
                throw new UnauthorizedAccessException("Chỉ gia sư của lớp mới được chèn link.");

            var created = new List<Media>();
            foreach (var (url, title) in links)
            {
                var m = new Media
                {
                    FileUrl = url,
                    FileName = string.IsNullOrWhiteSpace(title) ? "Link" : title!,
                    MediaType = DetectLinkType(url),
                    FileSize = 0,
                    OwnerUserId = tutorUserId,
                    Context = UploadContext.Material,
                    LessonId = lessonId
                };
                await _uow.Media.CreateAsync(m);
                created.Add(m);
            }
            await _uow.SaveChangesAsync();
            return created.Select(Map).ToList();
        }

        // ===== Delete (Tutor) =====
        public async Task<bool> DeleteAsync(string tutorUserId, string lessonId, string mediaId, CancellationToken ct)
        {
            var (lesson, cls) = await _uow.Lessons.GetWithClassAsync(lessonId);
            var tutorUserIdOfClass = await _uow.TutorProfiles.GetTutorUserIdByTutorProfileIdAsync(cls.TutorId);
            if (tutorUserIdOfClass != tutorUserId)
                throw new UnauthorizedAccessException("Chỉ gia sư của lớp mới được xoá tài liệu.");

            var media = await _uow.Media.GetAsync(m =>
                m.Id == mediaId &&
                m.LessonId == lessonId &&
                m.Context == UploadContext.Material &&
                m.DeletedAt == null
            );

            if (media == null) return false;

            // Delete from Cloudinary if it's an uploaded file (not a link)
            if (!string.IsNullOrWhiteSpace(media.ProviderPublicId))
            {
                await _storage.DeleteAsync(media.ProviderPublicId, media.MediaType, ct);
            }

            // Soft delete in database
            media.DeletedAt = DateTime.Now;
            await _uow.Media.UpdateAsync(media);
            await _uow.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// (Helper) Kiểm tra file có phải là video không
        /// </summary>
        private static bool IsVideoFile(string mediaType)
        {
            if (string.IsNullOrEmpty(mediaType))
                return false;

            var lowerType = mediaType.ToLower();
            return lowerType.StartsWith("video/") ||
                   lowerType.Contains("mp4") ||
                   lowerType.Contains("mov") ||
                   lowerType.Contains("webm") ||
                   lowerType.Contains("mkv") ||
                   lowerType.Contains("avi");
        }

        /// <summary>
        /// (Helper) Phân loại link (để UI biết cách hiển thị)
        /// </summary>
        private static string DetectLinkType(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (uri.Host.Contains("youtube.com") || uri.Host.Contains("youtu.be"))
                    return "link/youtube";
                if (uri.Host.Contains("drive.google.com"))
                    return "link/googledrive";

                return "link/url"; // Link chung
            }
            catch
            {
                return "link/url"; // Fallback nếu URL không hợp lệ (dù đã check)
            }
        }
    }
}
