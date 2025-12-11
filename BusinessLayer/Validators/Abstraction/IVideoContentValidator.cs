using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Validators.Abstraction
{
    /// <summary>
    /// Validator for video content
    /// </summary>
    public interface IVideoContentValidator
    {
        /// <summary>
        /// Validate video for inappropriate content, subject match, and education level
        /// </summary>
        Task<ValidationResult> ValidateVideoAsync(IFormFile videoFile, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default);
    }
}
