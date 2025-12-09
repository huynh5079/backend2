namespace BusinessLayer.DTOs.VideoAnalysis
{
    public class VideoAnalysisDto
    {
        public string Id { get; set; } = string.Empty;
        public string MediaId { get; set; } = string.Empty;
        public string LessonId { get; set; } = string.Empty;
        
        // Transcription
        public string? Transcription { get; set; }
        public string? TranscriptionLanguage { get; set; }
        
        // Summary
        public string? Summary { get; set; }
        public string? SummaryType { get; set; }
        public List<string>? KeyPoints { get; set; }
        
        // Status
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        
        // Metadata
        public int? VideoDurationSeconds { get; set; }
        public DateTime? AnalyzedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

