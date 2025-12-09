using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class VideoAnalysis : BaseEntity
{
    public string MediaId { get; set; } = default!; // Foreign key tới Media
    public string LessonId { get; set; } = default!; // Foreign key tới Lesson
    
    // Transcription
    public string? Transcription { get; set; } // Full text từ video
    public string? TranscriptionLanguage { get; set; } // Ngôn ngữ của transcription (vi, en, etc.)
    
    // Summary
    public string? Summary { get; set; } // Tóm tắt nội dung
    public string? SummaryType { get; set; } // concise, overview, key-insights
    public string? KeyPoints { get; set; } // JSON string chứa list các điểm quan trọng
    
    // Status
    public VideoAnalysisStatus Status { get; set; } = VideoAnalysisStatus.Pending;
    public string? ErrorMessage { get; set; } // Lỗi nếu có
    
    // Metadata
    public int? VideoDurationSeconds { get; set; } // Độ dài video (giây)
    public DateTime? AnalyzedAt { get; set; } // Thời gian phân tích xong
    
    // Navigation properties
    public virtual Media Media { get; set; } = default!;
    public virtual Lesson Lesson { get; set; } = default!;
}

public enum VideoAnalysisStatus
{
    Pending = 0,      // Đang chờ xử lý
    Processing = 1,   // Đang xử lý
    Completed = 2,    // Hoàn thành
    Failed = 3        // Thất bại
}

