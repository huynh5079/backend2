using BusinessLayer.DTOs.Quiz;
using BusinessLayer.DTOs.Validation;
using BusinessLayer.Validators.Abstraction;
using DataLayer.Enum;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text.Json;

namespace BusinessLayer.Validators
{
    public class TextContentValidator : ITextContentValidator
    {
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;

        public TextContentValidator(IConfiguration config)
        {
            _geminiApiKey = config["Gemini_Video:ApiKey"] 
                ?? throw new InvalidOperationException("Gemini API Key not configured");
            _geminiModel = config["Gemini_Video:Model"] ?? "gemini-pro";
        }

        public async Task<ValidationResult> ValidateQuizTextAsync(string textContent, string expectedSubject, CancellationToken ct = default)
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

IMPORTANT: Respond in VIETNAMESE language for all text fields.

OUTPUT JSON:
{{
  ""hasInappropriateContent"": <true|false>,
  ""inappropriateReason"": ""<lý do cụ thể bằng TIẾNG VIỆT nếu có, null nếu không>"",
  ""isSubjectMismatch"": <true|false>,
  ""detectedSubject"": ""<môn học phát hiện bằng TIẾNG VIỆT>"",
  ""matchingQuestionCount"": <number>,
  ""totalQuestionCount"": <number>,
  ""subjectMismatchReason"": ""<lý do không khớp bằng TIẾNG VIỆT nếu có, null nếu không>""
}}

QUIZ CONTENT:
{textContent}";
            
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
                Console.WriteLine($"Quiz Text Validation Error: {ex.Message}");
                return new ValidationResult { IsValid = true };
            }
        }

        public async Task<ValidationResult> ValidateDocumentTextAsync(string textContent, string expectedSubject, string expectedEducationLevel, CancellationToken ct = default)
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
Analyze this educational document for:
1. Inappropriate content (violence, offensive language, sexual content, hate speech, drugs)
2. Subject match - Expected: ""{expectedSubject}""
3. Education level match - Expected: ""{expectedEducationLevel}""

RULES:
- Flag if content is NOT about the expected subject (be flexible, related topics are OK)
- Flag if difficulty level doesn't match education level
- Consider context (e.g., ""review basics"" in advanced class is OK)
- Vietnamese education levels: ""Lớp 1"" to ""Lớp 12""

IMPORTANT: Respond in VIETNAMESE language for all text fields.

OUTPUT JSON ONLY:
{{
  ""hasInappropriateContent"": <true|false>,
  ""inappropriateReason"": ""<lý do cụ thể bằng TIẾNG VIỆT nếu có, null nếu không>"",
  ""isSubjectMismatch"": <true|false>,
  ""detectedSubject"": ""<môn học phát hiện bằng TIẾNG VIỆT>"",
  ""subjectMismatchReason"": ""<lý do không khớp bằng TIẾNG VIỆT nếu có, null nếu không>"",
  ""isEducationLevelMismatch"": <true|false>,
  ""detectedLevel"": ""<cấp độ học vấn phát hiện bằng TIẾNG VIỆT>"",
  ""levelMismatchReason"": ""<lý do không khớp bằng TIẾNG VIỆT nếu có, null nếu không>"",
  ""isEducational"": <true|false>,
  ""contentSummary"": ""<tóm tắt ngắn gọn bằng TIẾNG VIỆT (1-2 câu)>""
}}

DOCUMENT CONTENT:
{textContent}";

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

                var aiResponse = JsonSerializer.Deserialize<DocumentValidationResponse>(jsonText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (aiResponse == null)
                    throw new InvalidOperationException("Failed to parse AI response");

                // Check inappropriate content FIRST (highest priority)
                if (aiResponse.HasInappropriateContent)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Issue = ValidationIssue.InappropriateDocument,
                        ErrorMessage = $"Tài liệu chứa nội dung không phù hợp: {aiResponse.InappropriateReason}"
                    };
                }

                // Check subject mismatch
                if (aiResponse.IsSubjectMismatch)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Issue = ValidationIssue.SubjectMismatch,
                        ErrorMessage = $"Tài liệu không khớp môn học. Môn mong đợi: {expectedSubject}, phát hiện: {aiResponse.DetectedSubject}. {aiResponse.SubjectMismatchReason}"
                    };
                }

                // Check education level mismatch
                if (aiResponse.IsEducationLevelMismatch)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Issue = ValidationIssue.EducationLevelMismatch,
                        ErrorMessage = $"Nội dung không phù hợp cấp độ. Cấp độ mong đợi: {expectedEducationLevel}, phát hiện: {aiResponse.DetectedLevel}. {aiResponse.LevelMismatchReason}"
                    };
                }

                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                // Log error but allow upload if AI validation fails
                Console.WriteLine($"Document Text Validation Error: {ex.Message}");
                return new ValidationResult { IsValid = true };
            }
        }
    }
}
