using BusinessLayer.DTOs.VideoAnalysis;

namespace BusinessLayer.Service.Interface
{
    public interface IVideoAnalysisService
    {
        /// <summary>
        /// Phân tích video: transcribe và tóm tắt
        /// </summary>
        Task<VideoAnalysisDto> AnalyzeVideoAsync(string mediaId, string lessonId, string videoUrl, CancellationToken ct = default);

        /// <summary>
        /// Lấy kết quả phân tích video
        /// </summary>
        Task<VideoAnalysisDto?> GetAnalysisAsync(string mediaId, CancellationToken ct = default);

        /// <summary>
        /// Trả lời câu hỏi về video dựa trên transcription
        /// </summary>
        Task<VideoQuestionResponseDto> AnswerQuestionAsync(string mediaId, VideoQuestionRequestDto request, CancellationToken ct = default);

        /// <summary>
        /// Trigger phân tích lại video (nếu cần)
        /// </summary>
        Task<VideoAnalysisDto> ReanalyzeVideoAsync(string mediaId, CancellationToken ct = default);
    }
}

