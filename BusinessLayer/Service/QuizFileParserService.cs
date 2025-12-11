using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Service.Interface;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace BusinessLayer.Service
{
    public class QuizFileParserService : IQuizFileParserService
    {
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;
        private readonly float _temperature;

        public QuizFileParserService(IConfiguration configuration)
        {
            _geminiApiKey = configuration["Gemini_Video:ApiKey"]
                ?? throw new InvalidOperationException("Gemini API Key not configured");

            _geminiModel = configuration["Gemini_Video:Model"] ?? "gemini-1.5-flash";

            _temperature = float.Parse(configuration["Gemini_Video:Temperature"] ?? "0.1");
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
            if (extension != ".txt" && extension != ".docx" && extension != ".pdf")
                throw new ArgumentException("Only .txt, .docx, and .pdf files are supported");

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
            else if (extension == ".docx")
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
            else // .pdf
            {
                using var stream = file.OpenReadStream();
                using var pdfDocument = PdfDocument.Open(stream);
                
                var text = new StringBuilder();
                foreach (var page in pdfDocument.GetPages())
                {
                    text.AppendLine(page.Text);
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

            var systemInstruction = @"You are an intelligent quiz parser AI with LANGUAGE-AWARE capabilities.

        CRITICAL: LANGUAGE AUTO-DETECTION & MATCHING

        STEP 1: DETECT INPUT LANGUAGE
        - Analyze the quiz content to determine the PRIMARY language
        - Possible languages: Vietnamese (vi), English (en), or other languages
        - This detected language MUST be used for ALL output text fields

        STEP 2: PARSE CONTENT IN DETECTED LANGUAGE

        1. TITLE (required):
            - Use SAME LANGUAGE as input content
            - Vietnamese input → Vietnamese title
            Example: ""Bài kiểm tra Toán học lớp 10"", ""Quiz Lịch sử Việt Nam""
            - English input → English title
            Example: ""Grade 10 Mathematics Quiz"", ""World History Test""
            - Other language → Use that language

        Rules:
            - If file contains 'Title:', 'QUIZ:', 'Quiz:', 'Tiêu đề:' → extract it
            - If NOT found → CREATE a descriptive title IN DETECTED LANGUAGE
            - DON'T translate - keep original language

        2. DESCRIPTION:
            - Use SAME LANGUAGE as input content
            - Keep brief (1-2 sentences) or null
            - Vietnamese: ""Kiểm tra kiến thức..."", ""Bài tập ôn tập...""
            - English: ""Test on..."", ""Practice quiz...""
            - DON'T translate

        3. TIME LIMIT (minutes):
            - If specified → extract number
            - If NOT specified → SUGGEST based on:
                * 1-5 questions = 5-10 min
                * 6-10 questions = 10-15 min
                * 11-15 questions = 15-20 min
                * 16+ questions = 25+ min

        4. PASSING SCORE (percentage 0-100):
            - If specified → extract number
            - If NOT specified → SUGGEST:
                * Easy questions = 80%
                * Medium = 70%
                * Hard/technical = 60%

        5. QUESTIONS:
            - Keep in ORIGINAL LANGUAGE - DO NOT TRANSLATE
            - Extract questionText, options A/B/C/D exactly as written
            - Extract correct answer (A, B, C, or D)
            - Extract explanation if available (in SAME LANGUAGE)

        CRITICAL RULES:
            - ALL text fields (title, description, questionText, options, explanation) MUST use the SAME LANGUAGE as input
            - NEVER translate content
            - Maintain language consistency throughout entire output
            - If input is Vietnamese → output Vietnamese
            - If input is English → output English
            - If input is mixed → use the DOMINANT language

        OUTPUT FORMAT (JSON only):
        {
            ""title"": ""<in detected language>"",
            ""description"": ""<in detected language or null>"",
            ""timeLimit"": <number>,
            ""passingScore"": <number>,
            ""questions"": [
                {
                    ""questionText"": ""<in original language>"",
                    ""optionA"": ""<in original language>"",
                    ""optionB"": ""<in original language>"",
                    ""optionC"": ""<in original language>"",
                    ""optionD"": ""<in original language>"",
                    ""correctAnswer"": ""A"" (must be uppercase A, B, C, or D),
                    ""explanation"": ""<in original language or null>""
                }
            ]
        }

    VALIDATION:
    - correctAnswer must be uppercase A, B, C, or D
    - Return ONLY valid JSON
    - NO markdown, NO code blocks, just pure JSON";
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
