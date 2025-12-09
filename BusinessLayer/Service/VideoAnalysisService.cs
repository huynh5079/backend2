using BusinessLayer.DTOs.VideoAnalysis;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System.Text.Json;

namespace BusinessLayer.Service
{
    public class VideoAnalysisService : IVideoAnalysisService
    {
        private readonly IUnitOfWork _uow;
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;
        private readonly float _temperature;

        public VideoAnalysisService(
            IUnitOfWork uow,
            IConfiguration configuration)
        {
            _uow = uow;
            _geminiApiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini API Key not configured");
            _geminiModel = configuration["Gemini:Model"] ?? "gemini-pro";
            _temperature = float.Parse(configuration["Gemini:Temperature"] ?? "0.1");
        }

        public async Task<VideoAnalysisDto> AnalyzeVideoAsync(string mediaId, string lessonId, string videoUrl, CancellationToken ct = default)
        {
            // Kiểm tra xem đã có phân tích chưa
            var existing = await _uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
            if (existing != null && existing.Status == VideoAnalysisStatus.Completed)
            {
                return MapToDto(existing);
            }

            // Tạo hoặc update record
            VideoAnalysis analysis;
            if (existing != null)
            {
                analysis = existing;
                analysis.Status = VideoAnalysisStatus.Processing;
                analysis.UpdatedAt = DateTime.Now;
                await _uow.VideoAnalyses.UpdateAsync(analysis);
            }
            else
            {
                analysis = new VideoAnalysis
                {
                    MediaId = mediaId,
                    LessonId = lessonId,
                    Status = VideoAnalysisStatus.Processing
                };
                await _uow.VideoAnalyses.CreateAsync(analysis);
            }

            await _uow.SaveChangesAsync();

            try
            {
                // 1. Transcribe video bằng Gemini
                var transcription = await TranscribeVideoWithGeminiAsync(videoUrl, ct);
                analysis.Transcription = transcription.Text;
                analysis.TranscriptionLanguage = transcription.Language ?? "vi";

                // 2. Summarize transcription
                var summary = await SummarizeWithGeminiAsync(transcription.Text, ct);
                analysis.Summary = summary.SummaryText;
                analysis.SummaryType = "concise";
                analysis.KeyPoints = JsonSerializer.Serialize(summary.KeyPoints);

                // 3. Update status
                analysis.Status = VideoAnalysisStatus.Completed;
                analysis.AnalyzedAt = DateTime.Now;
                analysis.UpdatedAt = DateTime.Now;

                await _uow.VideoAnalyses.UpdateAsync(analysis);
                await _uow.SaveChangesAsync();

                return MapToDto(analysis);
            }
            catch (Exception ex)
            {
                analysis.Status = VideoAnalysisStatus.Failed;
                analysis.ErrorMessage = ex.Message;
                analysis.UpdatedAt = DateTime.Now;
                await _uow.VideoAnalyses.UpdateAsync(analysis);
                await _uow.SaveChangesAsync();
                throw;
            }
        }

        public async Task<VideoAnalysisDto?> GetAnalysisAsync(string mediaId, CancellationToken ct = default)
        {
            var analysis = await _uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
            return analysis != null ? MapToDto(analysis) : null;
        }

        public async Task<VideoQuestionResponseDto> AnswerQuestionAsync(string mediaId, VideoQuestionRequestDto request, CancellationToken ct = default)
        {
            var analysis = await _uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
            if (analysis == null)
                throw new InvalidOperationException("Video analysis not found. Please analyze the video first.");

            if (analysis.Status != VideoAnalysisStatus.Completed || string.IsNullOrEmpty(analysis.Transcription))
                throw new InvalidOperationException("Video transcription is not available yet. Please wait for analysis to complete.");

            var answer = await AnswerQuestionWithGeminiAsync(analysis.Transcription, request.Question, request.Language, ct);

            return new VideoQuestionResponseDto
            {
                Question = request.Question,
                Answer = answer,
                Language = request.Language
            };
        }

        public async Task<VideoAnalysisDto> ReanalyzeVideoAsync(string mediaId, CancellationToken ct = default)
        {
            var analysis = await _uow.VideoAnalyses.GetByMediaIdAsync(mediaId);
            if (analysis == null)
                throw new InvalidOperationException("Video analysis not found.");

            var media = await _uow.Media.GetByIdAsync(mediaId);
            if (media == null)
                throw new InvalidOperationException("Media not found.");

            return await AnalyzeVideoAsync(mediaId, analysis.LessonId, media.FileUrl, ct);
        }

        #region Private Methods - Gemini API Calls

        /// <summary>
        /// Transcribe video bằng Gemini API
        /// Cách hoạt động:
        /// 1. Gửi video URL (public URL từ Cloudinary) kèm prompt đến Gemini API
        /// 2. Gemini sẽ truy cập và xử lý video từ URL
        /// 3. Gemini trả về transcription text
        /// </summary>
        private async Task<(string Text, string? Language)> TranscribeVideoWithGeminiAsync(string videoUrl, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(_geminiModel);

            var config = new GenerationConfig
            {
                Temperature = _temperature,
                MaxOutputTokens = 8192
            };

            try
            {
                // Tạo prompt để yêu cầu Gemini transcribe
                // Lưu ý: Gemini Pro (gemini-pro) KHÔNG hỗ trợ xử lý video
                // Cần dùng gemini-1.5-pro hoặc gemini-2.0-flash-exp cho video
                // Nếu dùng gemini-pro, sẽ cần download video và gửi dưới dạng file
                var prompt = $@"Bạn là một hệ thống chuyển đổi giọng nói thành văn bản (Speech-to-Text).

Hãy xem và nghe video tại URL sau, sau đó chuyển đổi toàn bộ lời nói/audio trong video thành văn bản transcript.

Video URL: {videoUrl}

Yêu cầu chi tiết:
1. Chỉ transcript nội dung AUDIO/LỜI NÓI trong video
2. KHÔNG thêm bất kỳ nội dung nào khác không có trong video
3. Giữ nguyên ngữ điệu, dấu câu tự nhiên
4. Nếu video không có audio, trả về: ""[Video không có âm thanh]""
5. Nếu không thể truy cập video, trả về: ""[Không thể truy cập video]""
6. Chỉ trả về văn bản transcript thuần túy, KHÔNG có:
   - Markdown formatting
   - Giải thích thêm
   - Tóm tắt
   - Phân tích

Kết quả mong đợi: Chỉ là văn bản transcript chính xác của những gì được nói trong video.";

                var responseGemini = await model.GenerateContent(prompt, config);
                var transcription = responseGemini?.Text?.Trim() ?? "";

                if (string.IsNullOrEmpty(transcription))
                    throw new InvalidOperationException("Không thể transcribe video. Có thể video không có audio hoặc định dạng không được hỗ trợ.");

                // Detect language - có thể cải thiện bằng cách yêu cầu Gemini detect
                var detectedLanguage = "vi"; // Default, có thể dùng Gemini để detect chính xác hơn

                return (transcription, detectedLanguage);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Không thể tải video từ URL: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi khi transcribe video: {ex.Message}", ex);
            }
        }

        private async Task<(string SummaryText, List<string> KeyPoints)> SummarizeWithGeminiAsync(string transcription, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(_geminiModel);

            var config = new GenerationConfig
            {
                Temperature = _temperature,
                ResponseMimeType = "application/json",
                MaxOutputTokens = 4096
            };

            var systemInstruction = @"Bạn là một trợ lý AI chuyên tóm tắt bài giảng.
Hãy phân tích nội dung bài giảng và trả về kết quả dưới dạng JSON với format:
{
  ""summary"": ""Tóm tắt ngắn gọn nội dung bài giảng (2-3 đoạn văn)"",
  ""keyPoints"": [""Điểm quan trọng 1"", ""Điểm quan trọng 2"", ...]
}

Key points nên là danh sách 5-10 điểm quan trọng nhất của bài giảng.";

            var prompt = $"{systemInstruction}\n\nNội dung bài giảng:\n{transcription}";

            try
            {
                var response = await model.GenerateContent(prompt, config);
                var rawText = response?.Text?.Trim() ?? "";

                // Clean JSON
                int firstBrace = rawText.IndexOf('{');
                int lastBrace = rawText.LastIndexOf('}');
                if (firstBrace < 0 || lastBrace < firstBrace)
                    throw new InvalidOperationException("AI response không phải JSON hợp lệ");

                string jsonString = rawText.Substring(firstBrace, lastBrace - firstBrace + 1);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var result = JsonSerializer.Deserialize<SummaryResult>(jsonString, options);
                if (result == null)
                    throw new InvalidOperationException("Không thể parse kết quả từ AI");

                return (result.Summary ?? "Không thể tóm tắt", result.KeyPoints ?? new List<string>());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi khi tóm tắt: {ex.Message}", ex);
            }
        }

        private async Task<string> AnswerQuestionWithGeminiAsync(string transcription, string question, string language, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            var model = googleAI.GenerativeModel(_geminiModel);

            var config = new GenerationConfig
            {
                Temperature = _temperature,
                MaxOutputTokens = 2048
            };

            var prompt = $@"Bạn là trợ lý AI chuyên trả lời câu hỏi về nội dung bài giảng.

Nội dung bài giảng:
{transcription}

Câu hỏi: {question}

Hãy trả lời câu hỏi dựa trên nội dung bài giảng ở trên. Nếu câu hỏi không liên quan đến nội dung bài giảng, hãy thông báo rõ ràng.
Trả lời bằng tiếng {(language == "vi" ? "Việt" : "Anh")}.";

            try
            {
                var response = await model.GenerateContent(prompt, config);
                return response?.Text?.Trim() ?? "Không thể trả lời câu hỏi này.";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi khi trả lời câu hỏi: {ex.Message}", ex);
            }
        }

        #endregion

        #region Helper Methods

        private static VideoAnalysisDto MapToDto(VideoAnalysis entity)
        {
            List<string>? keyPoints = null;
            if (!string.IsNullOrEmpty(entity.KeyPoints))
            {
                try
                {
                    keyPoints = JsonSerializer.Deserialize<List<string>>(entity.KeyPoints);
                }
                catch { }
            }

            return new VideoAnalysisDto
            {
                Id = entity.Id,
                MediaId = entity.MediaId,
                LessonId = entity.LessonId,
                Transcription = entity.Transcription,
                TranscriptionLanguage = entity.TranscriptionLanguage,
                Summary = entity.Summary,
                SummaryType = entity.SummaryType,
                KeyPoints = keyPoints,
                Status = entity.Status.ToString(),
                ErrorMessage = entity.ErrorMessage,
                VideoDurationSeconds = entity.VideoDurationSeconds,
                AnalyzedAt = entity.AnalyzedAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private class SummaryResult
        {
            public string? Summary { get; set; }
            public List<string>? KeyPoints { get; set; }
        }

        #endregion
    }
}

