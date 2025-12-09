using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Service.Interface
{
    public interface IQuizContentValidatorService
    {
        /// <summary>
        /// Validate quiz file text content for inappropriate content and subject match
        /// </summary>
        Task<ValidationResult> ValidateTextAsync(
            string fileContent, 
            string expectedSubject, 
            CancellationToken ct = default);
        
        /// <summary>
        /// Validate uploaded image for inappropriate/violent/sensitive content
        /// </summary>
        Task<ValidationResult> ValidateImageAsync(
            IFormFile imageFile,
            CancellationToken ct = default);
    }
}
