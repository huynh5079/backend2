using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Service.Interface;
using BusinessLayer.Validators.Abstraction;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.Service
{
    public class MaterialContentValidatorService : IMaterialContentValidatorService
    {
        private readonly IDocumentContentValidator _documentValidator;
        private readonly IVideoContentValidator _videoValidator;
        private readonly IImageContentValidator _imageValidator;

        public MaterialContentValidatorService(IDocumentContentValidator documentValidator, IVideoContentValidator videoValidator, IImageContentValidator imageValidator)
        {
            _documentValidator = documentValidator;
            _videoValidator = videoValidator;
            _imageValidator = imageValidator;
        }

        public async Task<ValidationResult> ValidateFileAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default)
        {
            var extension = Path.GetExtension(file.FileName).ToLower();

            return extension switch
            {
                ".pdf" or ".docx" or ".txt" => await _documentValidator.ValidateDocumentAsync(
                    file, expectedSubject, expectedEducationLevel, ct),
                
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".mkv" => 
                    await _videoValidator.ValidateVideoAsync(
                        file, expectedSubject, expectedEducationLevel, ct),
                
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => 
                    await _imageValidator.ValidateImageAsync(file, ct),
                
                ".mp3" or ".wav" or ".wma" or ".ogg" or ".flac" => 
                    await ValidateAudioAsync(file, ct),
                
                _ => new ValidationResult { IsValid = true }
            };
        }

        public async Task<ValidationResult> ValidateDocumentAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default)
        {
            return await _documentValidator.ValidateDocumentAsync(file, expectedSubject, expectedEducationLevel, ct);
        }

        public async Task<ValidationResult> ValidateVideoAsync(IFormFile file, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default)
        {
            return await _videoValidator.ValidateVideoAsync(file, expectedSubject, expectedEducationLevel, ct);
        }

        public async Task<ValidationResult> ValidateImageAsync(IFormFile file, CancellationToken ct = default)
        {
            return await _imageValidator.ValidateImageAsync(file, ct);
        }

        // Audio validation - simple placeholder (no AI validation yet)
        private async Task<ValidationResult> ValidateAudioAsync(IFormFile file, CancellationToken ct = default)
        {
            // For now, just basic validation
            // Could be enhanced with AI audio analysis in the future
            return new ValidationResult { IsValid = true };
        }
    }
}
