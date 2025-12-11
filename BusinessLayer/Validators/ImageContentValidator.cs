using BusinessLayer.DTOs.Quiz;
using BusinessLayer.DTOs.Validation;
using BusinessLayer.Validators.Abstraction;
using DataLayer.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text.Json;

namespace BusinessLayer.Validators
{
    public class ImageContentValidator : IImageContentValidator
    {
        private readonly string _geminiApiKey;

        public ImageContentValidator(IConfiguration config)
        {
            _geminiApiKey = config["Gemini_Video:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini API Key not configured");
        }

        public async Task<ValidationResult> ValidateImageAsync(IFormFile imageFile, CancellationToken ct = default)
        {
            if (imageFile == null || imageFile.Length == 0)
                return new ValidationResult { IsValid = true };
            
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(Model.Gemini25Flash); // Vision-capable model
            
            var prompt = @"
Analyze this image for inappropriate content in educational context.

CHECK FOR:
- Nudity, sexual content
- Violence, gore, weapons
- Hate symbols, offensive gestures
- Drugs, alcohol
- Any content NOT suitable for educational environment

IMPORTANT: Respond in VIETNAMESE language.

OUTPUT JSON ONLY:
{
  ""isInappropriate"": <true|false>, 
  ""reason"": ""<lý do cụ thể bằng TIẾNG VIỆT nếu không phù hợp, null nếu phù hợp>""
}";
            
            try
            {
                // Save image to temp file
                var tempPath = Path.GetTempFileName();
                var extension = Path.GetExtension(imageFile.FileName);
                var imagePath = Path.ChangeExtension(tempPath, extension);
                
                using (var stream = new FileStream(imagePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream, ct);
                }
                
                try
                {
                    // Create request with image
                    var request = new GenerateContentRequest(prompt);
                    request.GenerationConfig = new GenerationConfig
                    {
                        Temperature = 0.1f,
                        ResponseMimeType = "application/json"
                    };
                    await request.AddMedia(imagePath);
                    
                    var response = await model.GenerateContent(request);
                    var jsonText = response?.Text?.Trim() ?? "";
                    
                    // Clean JSON
                    int firstBrace = jsonText.IndexOf('{');
                    int lastBrace = jsonText.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        jsonText = jsonText.Substring(firstBrace, lastBrace - firstBrace + 1);
                    }
                    
                    var aiResponse = JsonSerializer.Deserialize<ImageValidationResponse>(jsonText,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (aiResponse == null)
                        throw new InvalidOperationException("Failed to parse image validation response");
                    
                    if (aiResponse.IsInappropriate)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Issue = ValidationIssue.InappropriateImage,
                            ErrorMessage = $"Hình ảnh không phù hợp: {aiResponse.Reason}"
                        };
                    }
                    
                    return new ValidationResult { IsValid = true };
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but allow if AI validation fails
                Console.WriteLine($"Image Validation Error: {ex.Message}");
                return new ValidationResult { IsValid = true };
            }
        }
    }
}
