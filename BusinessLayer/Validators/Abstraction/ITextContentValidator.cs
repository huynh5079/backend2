using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Validators.Abstraction
{
    /// <summary>
    /// Validator for text content in quizzes and documents
    /// </summary>
    public interface ITextContentValidator
    {
        /// <summary>
        /// Validate quiz text content for inappropriate content and subject match
        /// </summary>
        Task<ValidationResult> ValidateQuizTextAsync(string textContent, string expectedSubject, CancellationToken ct = default);
        
        /// <summary>
        /// Validate document text content for inappropriate content, subject match, and education level
        /// </summary>
        Task<ValidationResult> ValidateDocumentTextAsync(string textContent, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default);
    }
}
