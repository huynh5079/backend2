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
    public class VideoContentValidator : IVideoContentValidator
    {
        private readonly string _geminiApiKey;

        public VideoContentValidator(IConfiguration config)
        {
            _geminiApiKey = config["Gemini_Video:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini API Key not configured");
        }

        public async Task<ValidationResult> ValidateVideoAsync(IFormFile videoFile, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default)
        {
            try
            {
                var googleAI = new GoogleAI(_geminiApiKey);
                var model = googleAI.GenerativeModel(Model.Gemini25Flash); // Vision-capable model

                var prompt = $@"
Analyze this educational video for:
1. Inappropriate content (nudity, violence, hate symbols, drugs, alcohol)
2. Subject match - Expected: ""{expectedSubject}""
3. Education level - Expected: ""{expectedEducationLevel}""

IMPORTANT: Respond in VIETNAMESE language for all text fields.

OUTPUT JSON ONLY:
{{
  ""isInappropriate"": <true|false>,
  ""reason"": ""<lý do cụ thể bằng TIẾNG VIỆT nếu không phù hợp, null nếu phù hợp>"",
  ""isSubjectMismatch"": <true|false>,
  ""detectedSubject"": ""<môn học phát hiện bằng TIẾNG VIỆT>"",
  ""isEducationLevelMismatch"": <true|false>,
  ""detectedLevel"": ""<cấp độ học vấn phát hiện bằng TIẾNG VIỆT>"",
  ""isEducational"": <true|false>,
  ""contentSummary"": ""<mô tả ngắn gọn nội dung video bằng TIẾNG VIỆT>""
}}";

                // Save video to temp file
                var tempPath = Path.GetTempFileName();
                var extension = Path.GetExtension(videoFile.FileName);
                var videoPath = Path.ChangeExtension(tempPath, extension);

                using (var stream = new FileStream(videoPath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(stream, ct);
                }

                try
                {
                    // Create request with video
                    var request = new GenerateContentRequest(prompt);
                    request.GenerationConfig = new GenerationConfig
                    {
                        Temperature = 0.1f,
                        ResponseMimeType = "application/json"
                    };
                    await request.AddMedia(videoPath);

                    var response = await model.GenerateContent(request);
                    var jsonText = response?.Text?.Trim() ?? "";

                    // Clean JSON
                    int firstBrace = jsonText.IndexOf('{');
                    int lastBrace = jsonText.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        jsonText = jsonText.Substring(firstBrace, lastBrace - firstBrace + 1);
                    }

                    var aiResponse = JsonSerializer.Deserialize<VideoValidationResponse>(jsonText,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (aiResponse == null)
                        throw new InvalidOperationException("Failed to parse video validation response");

                    // Check inappropriate content
                    if (aiResponse.IsInappropriate)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Issue = ValidationIssue.InappropriateVideo,
                            ErrorMessage = $"Video chứa nội dung không phù hợp: {aiResponse.Reason}"
                        };
                    }

                    // Check subject mismatch
                    if (aiResponse.IsSubjectMismatch)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Issue = ValidationIssue.SubjectMismatch,
                            ErrorMessage = $"Video không khớp môn học. Môn mong đợi: {expectedSubject}, phát hiện: {aiResponse.DetectedSubject}"
                        };
                    }

                    // Check education level mismatch
                    if (aiResponse.IsEducationLevelMismatch)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            Issue = ValidationIssue.EducationLevelMismatch,
                            ErrorMessage = $"Video không phù hợp cấp độ. Cấp độ mong đợi: {expectedEducationLevel}, phát hiện: {aiResponse.DetectedLevel}"
                        };
                    }

                    return new ValidationResult { IsValid = true };
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(videoPath))
                    {
                        File.Delete(videoPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but allow if AI validation fails
                Console.WriteLine($"Video Validation Error: {ex.Message}");
                return new ValidationResult { IsValid = true };
            }
        }
    }
}
