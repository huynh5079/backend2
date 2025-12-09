namespace BusinessLayer.DTOs.VideoAnalysis
{
    public class VideoQuestionRequestDto
    {
        public string Question { get; set; } = string.Empty;
        public string Language { get; set; } = "vi"; // Ngôn ngữ câu hỏi và câu trả lời
    }
}

