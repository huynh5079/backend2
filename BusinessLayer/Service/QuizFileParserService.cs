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

        public async Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default)
        {
            // 1. Validate file
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".txt" && extension != ".docx")
                throw new ArgumentException("Only .txt and .docx files are supported");

            // 2. Extract text from file
            return await ExtractTextFromFileAsync(file, extension, ct);
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

            var systemInstruction = @"You are an intelligent quiz parser AI. 
            Analyze the quiz content and extract/suggest information smartly:

            1. TITLE (required):
               - If file contains 'Title:', 'QUIZ:', 'Quiz:' → extract it
               - If NOT found → analyze questions and CREATE a descriptive title
               - Examples: 'C# Programming Quiz', 'Database Design Test', 'Advanced Algorithms'

            2. DESCRIPTION:
               - If found ('Description:', 'DESC:') → extract
               - If NOT found → create brief description (1-2 sentences) or use null

            3. TIME LIMIT (minutes):
               - If specified ('TIME:', 'Time Limit:') → extract number
                       - If NOT specified → SUGGEST based on:
                 * 1-5 questions = 5-10 min
                 * 6-10 questions = 10-15 min  
                 * 11-15 questions = 15-20 min
                 * 16+ questions = 25+ min
                 * Adjust for complexity (longer questions need more time)

            4. PASSING SCORE (percentage 0-100):
               - If specified ('PASS:', 'Passing Score:') → extract number
               - If NOT specified → SUGGEST based on difficulty:
                 * Easy/simple questions = 80%
                 * Medium complexity = 70%
                 * Hard/technical questions = 60%

            5. QUESTIONS: Extract all with A,B,C,D options, correct answer, explanation

            OUTPUT FORMAT (JSON only):
            {
              ""title"": ""string"",
              ""description"": ""string or null"",
              ""timeLimit"": ""TIME LIMIT"",
              ""passingScore"": ""PASSING SCORE"",
              ""questions"": [
                {
                  ""questionText"": ""string"",
                  ""optionA"": ""string"",
                  ""optionB"": ""string"",
                  ""optionC"": ""string"",
                  ""optionD"": ""string"",
                  ""correctAnswer"": ""A"" (must be A, B, C, or D),
                  ""explanation"": ""string or null""
                }
              ]
            }

            RULES:
            - ALWAYS provide title (extract or create)
            - ALWAYS provide timeLimit & passingScore (extract or suggest)
            - correctAnswer must be uppercase A, B, C, or D
            - Return ONLY valid JSON";
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
