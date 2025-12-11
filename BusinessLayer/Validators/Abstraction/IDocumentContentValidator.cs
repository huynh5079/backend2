using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Validators.Abstraction
{
    /// <summary>
    /// Validator for document content (PDF, DOCX, TXT)
    /// </summary>
    public interface IDocumentContentValidator
    {
        /// <summary>
        /// Validate document for inappropriate content, subject match, and education level
        /// </summary>
        Task<ValidationResult> ValidateDocumentAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default);
    }
}
