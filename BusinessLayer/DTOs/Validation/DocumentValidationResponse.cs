namespace BusinessLayer.DTOs.Validation
{
    public class DocumentValidationResponse
    {
        public bool HasInappropriateContent { get; set; }
        public string? InappropriateReason { get; set; }
        public bool IsSubjectMismatch { get; set; }
        public string? DetectedSubject { get; set; }
        public string? SubjectMismatchReason { get; set; }
        public bool IsEducationLevelMismatch { get; set; }
        public string? DetectedLevel { get; set; }
        public string? LevelMismatchReason { get; set; }
        public bool IsEducational { get; set; }
        public string? ContentSummary { get; set; }
    }
}
