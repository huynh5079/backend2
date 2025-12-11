namespace BusinessLayer.DTOs.Validation
{
    public class VideoValidationResponse
    {
        public bool IsInappropriate { get; set; }
        public string? Reason { get; set; }
        public bool IsSubjectMismatch { get; set; }
        public string? DetectedSubject { get; set; }
        public bool IsEducationLevelMismatch { get; set; }
        public string? DetectedLevel { get; set; }
        public bool IsEducational { get; set; }
        public string? ContentSummary { get; set; }
    }
}
