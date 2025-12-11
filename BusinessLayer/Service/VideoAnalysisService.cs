using BusinessLayer.DTOs.VideoAnalysis;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mscc.GenerativeAI;
using System.Text.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BusinessLayer.Service
{
    public class VideoAnalysisService : IVideoAnalysisService
    {
        private readonly IUnitOfWork _uow;
        private readonly string _geminiApiKey;
        private readonly string _geminiModel;
        private readonly float _temperature;
        private readonly HttpClient _httpClient;
        private readonly ILogger<VideoAnalysisService> _logger;

        public VideoAnalysisService(
            IUnitOfWork uow,
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<VideoAnalysisService> logger)
        {
            _uow = uow;
            // Đọc từ Gemini_Video section cho video analysis
            _geminiApiKey = configuration["Gemini_Video:ApiKey"]
                ?? throw new InvalidOperationException("Gemini_Video API Key not configured");
            
            // Validate API key format
            if (string.IsNullOrWhiteSpace(_geminiApiKey))
                throw new InvalidOperationException("Gemini_Video API Key is empty");
            
            if (!_geminiApiKey.StartsWith("AIza", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Gemini_Video API Key format is invalid. Should start with 'AIza', but got: {_geminiApiKey.Substring(0, Math.Min(10, _geminiApiKey.Length))}...");
            
            _geminiModel = configuration["Gemini_Video:Model"] ?? "gemini-2.5-flash";
            _temperature = float.Parse(configuration["Gemini_Video:Temperature"] ?? "0.1");
            _httpClient = httpClient;
            _logger = logger;
            
            // Debug: Log API key (chỉ hiển thị 15 ký tự đầu để bảo mật)
            var keyPreview = _geminiApiKey.Substring(0, Math.Min(15, _geminiApiKey.Length));
            _logger.LogInformation($"🔑 Gemini_Video API Key loaded: {keyPreview}...");
            // Log vào console để dễ thấy
            Console.WriteLine($"🔑 [VideoAnalysisService] Gemini_Video API Key: {keyPreview}...");
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
        /// 1. Download video từ URL (public URL từ Cloudinary)
        /// 2. Gửi video dưới dạng file data đến Gemini API (gemini-1.5-pro hỗ trợ video)
        /// 3. Gemini trả về transcription text
        /// </summary>
        private async Task<(string Text, string? Language)> TranscribeVideoWithGeminiAsync(string videoUrl, CancellationToken ct)
        {
            // Không dùng GoogleAI library cho video, dùng HTTP call trực tiếp
            // var googleAI = new GoogleAI(_geminiApiKey);
            // var model = googleAI.GenerativeModel(_geminiModel);

            var config = new GenerationConfig
            {
                Temperature = _temperature,
                MaxOutputTokens = 8192
            };

            try
            {
                // Download video từ URL
                byte[] videoBytes;
                try
                {
                    videoBytes = await _httpClient.GetByteArrayAsync(videoUrl, ct);
                    
                    // Kiểm tra kích thước video
                    // Gemini API hỗ trợ video lên đến ~2GB (tùy model và billing)
                    // Với billing enabled, có thể xử lý video lớn hơn
                    // Chỉ cảnh báo, không block nếu video > 100MB
                    var videoSizeMB = videoBytes.Length / (1024.0 * 1024.0);
                    
                    // Limit thực tế của Gemini API:
                    // - Free tier: ~20MB (có thể bị reject)
                    // - Paid tier với billing: có thể lên đến 2GB tùy model
                    // - gemini-1.5-flash: FREE, hỗ trợ video, quota cao hơn gấp 10 lần so với Pro
                    if (videoSizeMB > 2000)
                    {
                        throw new InvalidOperationException($"Video quá lớn ({videoSizeMB:F2} MB). Gemini API giới hạn video tối đa khoảng 2GB.");
                    }
                    
                    // Chỉ log warning cho video lớn, không block
                    if (videoSizeMB > 100)
                    {
                        // Log warning nhưng vẫn tiếp tục xử lý
                        System.Diagnostics.Debug.WriteLine($"Warning: Video lớn ({videoSizeMB:F2} MB) có thể mất nhiều thời gian và tốn nhiều tokens.");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Không thể tải video từ URL: {ex.Message}", ex);
                }

                // Upload video trực tiếp đến Gemini API qua HTTP
                // Gemini API cần video file được upload trực tiếp, không thể nhận URL trong prompt
                var transcription = await TranscribeVideoWithGeminiDirectUploadAsync(videoBytes, ct);

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

        /// <summary>
        /// Upload video trực tiếp đến Gemini API và nhận transcription
        /// Gọi Gemini API trực tiếp qua HTTP để upload file video
        /// </summary>
        private async Task<string> TranscribeVideoWithGeminiDirectUploadAsync(byte[] videoBytes, CancellationToken ct)
        {
            var prompt = @"Bạn là một hệ thống chuyển đổi giọng nói thành văn bản (Speech-to-Text).

Hãy xem và nghe video này, sau đó chuyển đổi toàn bộ lời nói/audio trong video thành văn bản transcript.

Yêu cầu chi tiết:
1. Chỉ transcript nội dung AUDIO/LỜI NÓI trong video
2. KHÔNG thêm bất kỳ nội dung nào khác không có trong video
3. Giữ nguyên ngữ điệu, dấu câu tự nhiên
4. Nếu video không có audio, trả về: ""[Video không có âm thanh]""
5. Chỉ trả về văn bản transcript thuần túy, KHÔNG có:
   - Markdown formatting
   - Giải thích thêm
   - Tóm tắt
   - Phân tích

Kết quả mong đợi: Chỉ là văn bản transcript chính xác của những gì được nói trong video.";

            try
            {
                // Gọi Gemini API trực tiếp qua HTTP
                // Dùng gemini-2.5-flash: Model mới nhất, hỗ trợ video, có trong v1
                // Gemini 1.5 series đã được thay thế bằng 2.5 series
                var modelName = "gemini-2.5-flash"; // Model mới nhất, hỗ trợ video tốt
                
                // Dùng v1 API (phiên bản ổn định)
                var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/{modelName}:generateContent?key={_geminiApiKey}";

                // Tạo request body với video file (base64 encoded)
                var requestBody = new Dictionary<string, object>
                {
                    ["contents"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["parts"] = new object[]
                            {
                                new Dictionary<string, string> { ["text"] = prompt },
                                new Dictionary<string, object>
                                {
                                    ["inlineData"] = new Dictionary<string, string>
                                    {
                                        ["mimeType"] = "video/mp4",
                                        ["data"] = Convert.ToBase64String(videoBytes)
                                    }
                                }
                            }
                        }
                    },
                    ["generationConfig"] = new Dictionary<string, object>
                    {
                        ["temperature"] = _temperature,
                        ["maxOutputTokens"] = 8192
                    }
                };

                var jsonBody = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content, ct);
                var responseContent = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    // Parse error response để có thông báo rõ ràng hơn
                    string errorMessage = responseContent;
                    try
                    {
                        using var errorDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                        var errorRoot = errorDoc.RootElement;
                        if (errorRoot.TryGetProperty("error", out var errorObj))
                        {
                            if (errorObj.TryGetProperty("message", out var message))
                                errorMessage = message.GetString() ?? responseContent;
                            
                            if (errorObj.TryGetProperty("status", out var status))
                                errorMessage = $"{status.GetString()}: {errorMessage}";
                        }
                    }
                    catch { }
                    
                    // Kiểm tra nếu là permission denied / suspended API key
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                        errorMessage.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase) ||
                        errorMessage.Contains("suspended", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"🚫 Gemini API Key đã bị đình chỉ (suspended) hoặc không có quyền truy cập.\n\n" +
                            $"Nguyên nhân có thể:\n" +
                            $"1. API key bị Google suspend do vi phạm Terms of Service\n" +
                            $"2. Quota đã vượt mức cho phép\n" +
                            $"3. Vấn đề về billing/payment\n" +
                            $"4. API key bị lộ hoặc bị abuse\n\n" +
                            $"Cách khắc phục:\n" +
                            $"1. Kiểm tra Google Cloud Console: https://console.cloud.google.com/\n" +
                            $"2. Vào APIs & Services → Credentials để xem trạng thái API key\n" +
                            $"3. Tạo API key mới nếu cần\n" +
                            $"4. Enable Generative Language API cho project\n" +
                            $"5. Kiểm tra billing account và credit còn lại\n" +
                            $"6. Cập nhật API key mới vào appsettings.json hoặc User Secrets\n\n" +
                            $"Chi tiết lỗi: {errorMessage}");
                    }
                    
                    // Kiểm tra nếu là quota error
                    if (IsQuotaError(responseContent, response.StatusCode))
                    {
                        throw new InvalidOperationException(
                            $"⚠️ Đã hết quota/token Gemini API khi transcribe video. " +
                            $"Với billing enabled ($300 credit), bạn có quota cao hơn nhiều:\n" +
                            $"- Requests: Không giới hạn (theo billing plan)\n" +
                            $"- Video size: Up to 2GB\n" +
                            $"- Tokens: Theo billing plan\n" +
                            $"Nếu vẫn gặp lỗi này:\n" +
                            $"1. Kiểm tra billing account đã được link với project chưa\n" +
                            $"2. Kiểm tra Generative Language API đã được enable chưa\n" +
                            $"3. Đợi vài phút để billing được activate\n" +
                            $"4. Sử dụng video nhỏ hơn để test\n" +
                            $"Chi tiết lỗi: {errorMessage}");
                    }
                    
                    throw new InvalidOperationException($"Gemini API error ({response.StatusCode}): {errorMessage}");
                }

                // Parse response
                using var doc = System.Text.Json.JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    throw new InvalidOperationException("Gemini API không trả về kết quả hợp lệ");

                var firstCandidate = candidates[0];
                if (!firstCandidate.TryGetProperty("content", out var contentObj))
                    throw new InvalidOperationException("Gemini API response thiếu content");

                if (!contentObj.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                    throw new InvalidOperationException("Gemini API response thiếu parts");

                var transcription = parts[0].GetProperty("text").GetString()?.Trim() ?? "";

                if (string.IsNullOrEmpty(transcription))
                    throw new InvalidOperationException("Không thể transcribe video. Có thể video không có audio hoặc định dạng không được hỗ trợ.");

                return transcription;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi khi upload và transcribe video: {ex.Message}", ex);
            }
        }

        private async Task<(string SummaryText, List<string> KeyPoints)> SummarizeWithGeminiAsync(string transcription, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            // Dùng gemini-2.5-flash thay vì _geminiModel (có thể còn cũ)
            var model = googleAI.GenerativeModel("gemini-2.5-flash");

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
                HandleGeminiApiError(ex, "tóm tắt transcription");
                throw; // Unreachable, nhưng để compiler happy
            }
        }

        private async Task<string> AnswerQuestionWithGeminiAsync(string transcription, string question, string language, CancellationToken ct)
        {
            var googleAI = new GoogleAI(_geminiApiKey);
            // Dùng gemini-2.5-flash thay vì _geminiModel (có thể còn cũ)
            var model = googleAI.GenerativeModel("gemini-2.5-flash");

            var config = new GenerationConfig
            {
                Temperature = _temperature,
                MaxOutputTokens = 4096 // Tăng từ 2048 để đảm bảo có đủ token cho câu trả lời dài
            };

            // Giới hạn độ dài transcription để tránh vượt token limit
            // Gemini 2.5-flash có context window ~1M tokens, nhưng để an toàn chỉ dùng ~50k chars (~12k tokens)
            const int maxTranscriptionLength = 50000;
            var truncatedTranscription = transcription;
            if (transcription.Length > maxTranscriptionLength)
            {
                truncatedTranscription = transcription.Substring(0, maxTranscriptionLength) + "... [Transcription đã được cắt ngắn]";
                _logger.LogWarning($"⚠️ Transcription quá dài ({transcription.Length} chars), đã cắt xuống {maxTranscriptionLength} chars");
            }

            var prompt = $@"Bạn là trợ lý AI chuyên trả lời câu hỏi về nội dung bài giảng.

Nội dung bài giảng:
{truncatedTranscription}

Câu hỏi: {question}

Hãy trả lời câu hỏi dựa trên nội dung bài giảng ở trên. Nếu câu hỏi không liên quan đến nội dung bài giảng, hãy thông báo rõ ràng.
Trả lời bằng tiếng {(language == "vi" ? "Việt" : "Anh")}.

QUAN TRỌNG: 
- Trả lời bằng văn bản thuần (plain text), KHÔNG dùng markdown formatting (không dùng *, **, #, ##, etc.)
- Không dùng ký tự đặc biệt để format như bold, italic, heading
- Chỉ dùng xuống dòng (\n) để phân đoạn, không dùng các ký tự markdown khác
- Trả lời tự nhiên, dễ đọc, không cần format đặc biệt";

            try
            {
                _logger.LogInformation($"Đang gọi Gemini API để trả lời câu hỏi. Question length: {question?.Length ?? 0}, Transcription length: {truncatedTranscription.Length}");
                
                var response = await model.GenerateContent(prompt, config);
                
                // Kiểm tra response và text
                if (response == null)
                {
                    _logger.LogError("Gemini API trả về null response");
                    throw new InvalidOperationException("Gemini API trả về null response");
                }
                
                var answerText = response.Text?.Trim();
                if (string.IsNullOrEmpty(answerText))
                {
                    // Log chi tiết để debug
                    _logger.LogError($"⚠️ Gemini API response.Text is null or empty. Response type: {response.GetType().Name}, Question: {question}, Transcription length: {transcription.Length}, Truncated length: {truncatedTranscription.Length}");
                    
                    // Thử log thêm thông tin về response nếu có
                    try
                    {
                        var responseString = response.ToString();
                        _logger.LogError($"Response object: {responseString?.Substring(0, Math.Min(500, responseString?.Length ?? 0))}");
                    }
                    catch
                    {
                        // Ignore nếu không thể convert response thành string
                    }
                    
                    throw new InvalidOperationException("Gemini API không trả về câu trả lời. Có thể do transcription quá dài, câu hỏi không hợp lệ, hoặc bị content filter chặn.");
                }
                
                // Làm sạch markdown formatting nếu có
                answerText = CleanMarkdownFormatting(answerText);
                
                _logger.LogInformation($"✅ Gemini API trả về answer thành công. Answer length: {answerText.Length}");
                return answerText;
            }
            catch (Exception ex)
            {
                // Log chi tiết để debug
                _logger.LogError(ex, $"❌ Error in AnswerQuestionWithGeminiAsync: {ex.Message}. Question: {question}, Transcription length: {transcription.Length}");
                
                HandleGeminiApiError(ex, "trả lời câu hỏi");
                throw; // Unreachable, nhưng để compiler happy
            }
        }

        #endregion

        #region Helper Methods - Text Processing

        /// <summary>
        /// Làm sạch markdown formatting từ text, giữ lại nội dung thuần
        /// </summary>
        private string CleanMarkdownFormatting(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Loại bỏ các ký tự markdown phổ biến
            var cleaned = text;
            
            // Loại bỏ ** (bold)
            cleaned = Regex.Replace(cleaned, @"\*\*([^*]+)\*\*", "$1");
            
            // Loại bỏ * (italic) - chỉ khi không phải là **
            cleaned = Regex.Replace(cleaned, @"(?<!\*)\*([^*]+)\*(?!\*)", "$1");
            
            // Loại bỏ # (headings)
            cleaned = Regex.Replace(cleaned, @"^#+\s*", "", RegexOptions.Multiline);
            
            // Loại bỏ __ (underline trong markdown)
            cleaned = Regex.Replace(cleaned, @"__([^_]+)__", "$1");
            
            // Loại bỏ _ (italic) - chỉ khi không phải là __
            cleaned = Regex.Replace(cleaned, @"(?<!_)_([^_]+)_(?!_)", "$1");
            
            // Loại bỏ ` (code blocks)
            cleaned = Regex.Replace(cleaned, @"`([^`]+)`", "$1");
            
            // Loại bỏ ~~ (strikethrough)
            cleaned = Regex.Replace(cleaned, @"~~([^~]+)~~", "$1");
            
            // Loại bỏ các ký tự markdown còn sót lại (như * đơn lẻ không pair)
            cleaned = Regex.Replace(cleaned, @"(?<!\*)\*(?!\*)", "");
            
            // Giữ lại \n (newline) nhưng normalize
            cleaned = cleaned.Replace("\r\n", "\n").Replace("\r", "\n");
            
            // Loại bỏ khoảng trắng thừa ở đầu/cuối mỗi dòng (nhưng giữ lại dòng trống)
            cleaned = Regex.Replace(cleaned, @"[ \t]+(\n|$)", "$1"); // Loại bỏ trailing spaces
            cleaned = Regex.Replace(cleaned, @"(\n|^)[ \t]+", "$1"); // Loại bỏ leading spaces
            
            // Loại bỏ các dòng trống thừa (giữ lại tối đa 2 dòng trống liên tiếp)
            cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
            
            // Loại bỏ dòng trống ở đầu và cuối
            cleaned = cleaned.Trim();
            
            // Đảm bảo format đẹp: thêm khoảng trắng sau dấu chấm, dấu phẩy nếu thiếu (nhưng không thêm vào cuối dòng hoặc sau số)
            // Chỉ thêm nếu không phải là số (ví dụ: 3.14 không thành 3. 14)
            cleaned = Regex.Replace(cleaned, @"([.,;:])([^\s\n\d])", "$1 $2");
            
            return cleaned;
        }

        #endregion

        #region Helper Methods - Error Handling

        /// <summary>
        /// Kiểm tra và xử lý lỗi quota/token exhaustion từ Gemini API
        /// </summary>
        private void HandleGeminiApiError(Exception ex, string operation)
        {
            var errorMessage = ex.Message;
            var innerException = ex.InnerException?.Message ?? "";

            // Kiểm tra các dấu hiệu của quota/token exhaustion
            var isQuotaError = errorMessage.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                              errorMessage.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                              errorMessage.Contains("Quota exceeded", StringComparison.OrdinalIgnoreCase) ||
                              errorMessage.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                              errorMessage.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                              errorMessage.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
                              innerException.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                              innerException.Contains("quota", StringComparison.OrdinalIgnoreCase);

            if (isQuotaError)
            {
                throw new InvalidOperationException(
                    $"⚠️ Đã hết quota/token Gemini API khi {operation}. " +
                    $"Với billing enabled ($300 credit), quota sẽ cao hơn nhiều:\n" +
                    $"- Requests: Không giới hạn nghiêm ngặt (theo billing plan)\n" +
                    $"- Video size: Up to 2GB\n" +
                    $"- Tokens: Theo billing plan\n" +
                    $"Nếu vẫn gặp lỗi này:\n" +
                    $"1. Kiểm tra billing account đã được link với project chưa\n" +
                    $"2. Kiểm tra Generative Language API đã được enable chưa\n" +
                    $"3. Đợi vài phút để billing được activate\n" +
                    $"4. Kiểm tra credit còn lại trong Google Cloud Console\n" +
                    $"Chi tiết lỗi: {errorMessage}", ex);
            }

            // Nếu không phải quota error, throw lại exception gốc
            throw new InvalidOperationException($"Lỗi khi {operation}: {errorMessage}", ex);
        }

        /// <summary>
        /// Kiểm tra response có phải là quota error không
        /// </summary>
        private bool IsQuotaError(string responseContent, System.Net.HttpStatusCode statusCode)
        {
            if (statusCode == System.Net.HttpStatusCode.TooManyRequests)
                return true;

            if (string.IsNullOrEmpty(responseContent))
                return false;

            return responseContent.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                   responseContent.Contains("\"status\":\"RESOURCE_EXHAUSTED\"", StringComparison.OrdinalIgnoreCase) ||
                   responseContent.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                   responseContent.Contains("Quota exceeded", StringComparison.OrdinalIgnoreCase) ||
                   responseContent.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
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

