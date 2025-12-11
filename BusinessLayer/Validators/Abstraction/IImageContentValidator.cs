using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Validators.Abstraction
{
    /// <summary>
    /// Validator for image content (quiz questions and lesson materials)
    /// </summary>
    public interface IImageContentValidator
    {
        /// <summary>
        /// Validate image for inappropriate content
        /// </summary>
        Task<ValidationResult> ValidateImageAsync(IFormFile imageFile, CancellationToken ct = default);
    }
}
