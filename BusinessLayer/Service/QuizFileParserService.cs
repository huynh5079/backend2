using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Service.Interface;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text;
using System.Text.Json;

namespace BusinessLayer.Service
{
    public class QuizFileParserService : IQuizFileParserService
    {
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;
        private readonly float _temperature;

        public QuizFileParserService(IConfiguration configuration)
        {
            _geminiApiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini API Key not configured");

            _geminiModel = configuration["Gemini:Model"] ?? "gemini-2.5-flash";

            _temperature = float.Parse(configuration["Gemini:Temperature"] ?? "0.1");
        }

        public async Task<ParsedQuizDto> ParseFileAsync(IFormFile file, CancellationToken ct = default)
        {
            // 1. Validate file
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".txt" && extension != ".docx")
                throw new ArgumentException("Only .txt and .docx files are supported");

            // 2. Extract text from file
            string fileContent = await ExtractTextFromFileAsync(file, extension, ct);

            // 3. Parse with Gemini AI
            var parsedQuiz = await ParseWithGeminiAsync(fileContent, ct);

            return parsedQuiz;
        }

        private async Task<string> ExtractTextFromFileAsync(IFormFile file, string extension, CancellationToken ct)
        {
            if (extension == ".txt")
            {
                using var reader = new StreamReader(file.OpenReadStream());
                return await reader.ReadToEndAsync(ct);
            }
            else // .docx
            {
                using var stream = file.OpenReadStream();
                using var doc = WordprocessingDocument.Open(stream, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null)
                    throw new InvalidOperationException("Cannot read DOCX content");

                var text = new StringBuilder();
                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    text.AppendLine(paragraph.InnerText);
                }
                return text.ToString();
            }
        }

        /*private async Task<ParsedQuizDto> ParseWithGeminiAsync(string fileContent, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(_geminiModel);

            // System instruction for AI
            var systemInstruction = @"You are a quiz parser. Extract quiz information from the provided text.
The text may be in any format. Your task is to identify:
- Quiz title
- Description (if any)
- Time limit in minutes (default to 0 if not specified)
- Passing score as percentage 0-100 (default to 70 if not specified)
- Questions with 4 options (A, B, C, D) and correct answer
- Explanation for each question (if provided)

Return ONLY valid JSON in this exact format:
{
  ""title"": ""Quiz Title"",
  ""description"": ""Description or null"",
  ""timeLimit"": 30,
  ""passingScore"": 70,
  ""questions"": [
    {
      ""questionText"": ""Question text?"",
      ""optionA"": ""Option A text"",
      ""optionB"": ""Option B text"",
      ""optionC"": ""Option C text"",
      ""optionD"": ""Option D text"",
      ""correctAnswer"": ""A"",
      ""explanation"": ""Explanation or null""
    }
  ]
}

IMPORTANT: 
- correctAnswer must be exactly 'A', 'B', 'C', or 'D' (uppercase)
- Return ONLY the JSON object, no markdown, no explanations";

            var prompt = $"{systemInstruction}\n\nParse this quiz:\n\n{fileContent}";

            var response = await Task.Run(() => model.GenerateContent(prompt), ct);
            var jsonResponse = response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim() ?? "";

            // Remove markdown code blocks if present
            if (jsonResponse.StartsWith("```json"))
                jsonResponse = jsonResponse.Substring(7);
            if (jsonResponse.StartsWith("```"))
                jsonResponse = jsonResponse.Substring(3);
            if (jsonResponse.EndsWith("```"))
                jsonResponse = jsonResponse.Substring(0, jsonResponse.Length - 3);
            jsonResponse = jsonResponse.Trim();

            // Parse JSON
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var parsed = JsonSerializer.Deserialize<ParsedQuizDto>(jsonResponse, options);
                
                if (parsed == null || parsed.Questions == null || !parsed.Questions.Any())
                    throw new InvalidOperationException("Failed to parse quiz: No questions found");

                // Validate
                foreach (var q in parsed.Questions)
                {
                    if (q.CorrectAnswer != 'A' && q.CorrectAnswer != 'B' && q.CorrectAnswer != 'C' && q.CorrectAnswer != 'D')
                        throw new InvalidOperationException($"Invalid correct answer: {q.CorrectAnswer}");
                }

                return parsed;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse Gemini response as JSON: {ex.Message}. Response was: {jsonResponse}");
            }
        }*/

        private async Task<ParsedQuizDto> ParseWithGeminiAsync(string fileContent, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(_geminiModel);

            var config = new GenerationConfig
            {
                Temperature = _temperature,
                ResponseMimeType = "application/json",
                MaxOutputTokens = 8192
            };

            var systemInstruction = @"You are a strict quiz parser.
OUTPUT FORMAT: JSON only.
SCHEMA:
{
  ""title"": ""string"",
  ""description"": ""string"",
  ""timeLimit"": 30,
  ""passingScore"": 70,
  ""questions"": [
    {
      ""questionText"": ""string"",
      ""optionA"": ""string"",
      ""optionB"": ""string"",
      ""optionC"": ""string"",
      ""optionD"": ""string"",
      ""correctAnswer"": ""A"" (must be A, B, C, or D),
      ""explanation"": ""string""
    }
  ]
}";
            var prompt = $"{systemInstruction}\n\nCONTENT TO PARSE:\n{fileContent}";

            try
            {
                // Gọi API (Bỏ ct nếu thư viện phiên bản cũ không hỗ trợ)
                var response = await model.GenerateContent(prompt, config);
                var rawText = response?.Text?.Trim();

                if (string.IsNullOrEmpty(rawText))
                    throw new InvalidOperationException("Gemini returned empty response");

                // --- LOGIC LÀM SẠCH JSON (QUAN TRỌNG) ---
                // Tìm vị trí bắt đầu '{' và kết thúc '}' để loại bỏ chữ thừa
                int firstBrace = rawText.IndexOf('{');
                int lastBrace = rawText.LastIndexOf('}');

                if (firstBrace < 0 || lastBrace < firstBrace)
                {
                    throw new InvalidOperationException($"AI response does not contain valid JSON. Response: {rawText}");
                }

                // Cắt lấy đúng phần JSON
                string jsonString = rawText.Substring(firstBrace, lastBrace - firstBrace + 1);

                // Cấu hình JSON cho phép lỗi nhỏ (dấu phẩy thừa, comment)
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var parsed = JsonSerializer.Deserialize<ParsedQuizDto>(jsonString, options);

                if (parsed == null || parsed.Questions == null || !parsed.Questions.Any())
                    throw new InvalidOperationException("No questions found in parsed data.");

                // Validate CorrectAnswer (A, B, C, D)
                var validAnswers = new HashSet<char> { 'A', 'B', 'C', 'D' };
                foreach (var q in parsed.Questions)
                {
                    q.CorrectAnswer = char.ToUpper(q.CorrectAnswer);
                    if (!validAnswers.Contains(q.CorrectAnswer))
                        throw new InvalidOperationException($"Invalid answer '{q.CorrectAnswer}' detected.");
                }

                return parsed;
            }
            catch (JsonException jEx)
            {
                throw new InvalidOperationException($"JSON Parsing failed. Please check file format. Details: {jEx.Message}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"AI Processing Error: {ex.Message}");
            }
        }
    }
}
