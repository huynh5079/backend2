using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Service.Interface
{
    public interface IMaterialContentValidatorService
    {
        /// <summary>
        /// Validate document file (PDF, DOCX, TXT) for inappropriate content, subject match, and education level
        /// </summary>
        Task<ValidationResult> ValidateDocumentAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default);

        /// <summary>
        /// Validate video file for inappropriate content, subject match, and education level
        /// </summary>
        Task<ValidationResult> ValidateVideoAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default);

        /// <summary>
        /// Validate image file for inappropriate content (delegates to QuizContentValidator)
        /// </summary>
        Task<ValidationResult> ValidateImageAsync(IFormFile file, CancellationToken ct = default);

        /// <summary>
        /// Auto-detect file type and validate accordingly
        /// </summary>
        Task<ValidationResult> ValidateFileAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default);
    }
}