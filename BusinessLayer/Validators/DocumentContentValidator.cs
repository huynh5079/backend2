using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Validators.Abstraction;
using DataLayer.Enum;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace BusinessLayer.Validators
{
    public class DocumentContentValidator : IDocumentContentValidator
    {
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;
        private readonly ITextContentValidator _textValidator;

        public DocumentContentValidator(IConfiguration config, ITextContentValidator textValidator)
        {
            _geminiApiKey = config["Gemini_Video:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini API Key not configured");
            _geminiModel = config["Gemini_Video:Model"] ?? "gemini-pro";
            _textValidator = textValidator;
        }

        public async Task<ValidationResult> ValidateDocumentAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default)
        {
            try
            {
                var extension = Path.GetExtension(file.FileName).ToLower();
                
                // Extract text from document
                string textContent = await ExtractTextFromDocumentAsync(file, extension, ct);

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    // Empty document - allow but could be flagged
                    return new ValidationResult { IsValid = true };
                }

                // Delegate to TextContentValidator for validation
                return await _textValidator.ValidateDocumentTextAsync(
                    textContent,
                    expectedSubject,
                    expectedEducationLevel,
                    ct);
            }
            catch (Exception ex)
            {
                // Log error but allow upload if validation fails
                Console.WriteLine($"Document Validation Error: {ex.Message}");
                return new ValidationResult { IsValid = true };
            }
        }

        private async Task<string> ExtractTextFromDocumentAsync(
            IFormFile file, 
            string extension, 
            CancellationToken ct = default)
        {
            return extension switch
            {
                ".pdf" => await ExtractFromPdfAsync(file, ct),
                ".docx" => await ExtractFromDocxAsync(file, ct),
                ".txt" => await ExtractFromTxtAsync(file, ct),
                _ => string.Empty
            };
        }

        private async Task<string> ExtractFromPdfAsync(IFormFile file, CancellationToken ct = default)
        {
            using var stream = file.OpenReadStream();
            using var pdfDocument = UglyToad.PdfPig.PdfDocument.Open(stream);
            var text = new StringBuilder();

            foreach (var page in pdfDocument.GetPages())
            {
                text.AppendLine(page.Text);
            }

            return text.ToString();
        }

        private async Task<string> ExtractFromDocxAsync(IFormFile file, CancellationToken ct = default)
        {
            using var stream = file.OpenReadStream();
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document?.Body;

            if (body == null) return string.Empty;

            var text = new StringBuilder();
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                text.AppendLine(paragraph.InnerText);
            }

            return text.ToString();
        }

        private async Task<string> ExtractFromTxtAsync(IFormFile file, CancellationToken ct = default)
        {
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync();
        }
    }
}
