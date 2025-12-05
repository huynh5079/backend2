using BusinessLayer.DTOs.Quiz;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Service.Interface
{
    public interface IQuizFileParserService
    {
        /// <summary>
        /// Parse quiz file (.txt or .docx) using Gemini AI to extract questions and answers
        /// </summary>
        Task<ParsedQuizDto> ParseFileAsync(IFormFile file, CancellationToken ct = default);
    }
}
