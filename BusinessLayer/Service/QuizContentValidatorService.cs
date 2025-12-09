using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Service.Interface;
using DataLayer.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text.Json;

namespace BusinessLayer.Service
{
    public class QuizContentValidatorService : IQuizContentValidatorService
    {
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;
        
        public QuizContentValidatorService(IConfiguration config)
        {
            _geminiApiKey = config["Gemini:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini API Key not configured");
            _geminiModel = config["Gemini:Model"] ?? "gemini-pro";
        }
        
        public async Task<ValidationResult> ValidateTextAsync(
            string fileContent, 
            string expectedSubject,
            CancellationToken ct = default)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(_geminiModel);
            
            var config = new GenerationConfig
            {
                Temperature = 0.1f,
                ResponseMimeType = "application/json",
                MaxOutputTokens = 2048
            };
            
            var prompt = $@"
Validate quiz content for:
1. Inappropriate content (offensive language, violence, sexual content, hate speech)
2. Subject match (Expected: ""{expectedSubject}"")

Rules:
- Be context-aware (e.g., ""damn good"" is OK, ""you are damned"" is NOT OK)
- Analyze ALL questions in the quiz
- ONLY flag subject mismatch if MAJORITY (>50%) of questions are about different subject
- Ignore 1-2 typos or ambiguous questions

OUTPUT JSON:
{{
  ""hasInappropriateContent"": <true|false>,
  ""inappropriateReason"": ""<specific reason if true, null otherwise>"",
  ""isSubjectMismatch"": <true|false>,
  ""detectedSubject"": ""<primary subject detected>"",
  ""matchingQuestionCount"": <number>,
  ""totalQuestionCount"": <number>,
  ""subjectMismatchReason"": ""<reason if mismatch, null otherwise>""
}}

QUIZ CONTENT:
{fileContent}";
            
            try
            {
                var response = await model.GenerateContent(prompt, config);
                var jsonText = response?.Text?.Trim() ?? "";
                
                // Clean JSON
                int firstBrace = jsonText.IndexOf('{');
                int lastBrace = jsonText.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    jsonText = jsonText.Substring(firstBrace, lastBrace - firstBrace + 1);
                }
                
                var aiResponse = JsonSerializer.Deserialize<TextValidationResponse>(jsonText, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (aiResponse == null)
                    throw new InvalidOperationException("Failed to parse AI response");
                
                // Check inappropriate content FIRST (higher priority)
                if (aiResponse.HasInappropriateContent)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Issue = ValidationIssue.InappropriateContent,
                        ErrorMessage = $"Nội dung không phù hợp: {aiResponse.InappropriateReason}"
                    };
                }
                
                // Check subject mismatch
                if (aiResponse.IsSubjectMismatch)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Issue = ValidationIssue.SubjectMismatch,
                        ErrorMessage = $"Quiz không khớp môn '{expectedSubject}'. " +
                                     $"Phát hiện: {aiResponse.DetectedSubject}. " +
                                     $"({aiResponse.MatchingQuestionCount}/{aiResponse.TotalQuestionCount} câu khớp). " +
                                     $"{aiResponse.SubjectMismatchReason}"
                    };
                }
                
                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                // Log error but allow upload if AI validation fails (network issue, etc)
                Console.WriteLine($"Text Validation Error: {ex.Message}");
                return new ValidationResult { IsValid = true };
            }
        }
        
        public async Task<ValidationResult> ValidateImageAsync(
            IFormFile imageFile,
            CancellationToken ct = default)
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

OUTPUT JSON ONLY:
{
  ""isInappropriate"": <true|false>, 
  ""reason"": ""<specific reason if inappropriate, null otherwise>""
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
