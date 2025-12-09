namespace BusinessLayer.DTOs.Quiz
{
    /// <summary>
    /// AI response for text content validation
    /// </summary>
    public class TextValidationResponse
    {
        public bool HasInappropriateContent { get; set; }
        public string? InappropriateReason { get; set; }
        public bool IsSubjectMismatch { get; set; }
        public string DetectedSubject { get; set; } = "";
        public int MatchingQuestionCount { get; set; }
        public int TotalQuestionCount { get; set; }
        public string? SubjectMismatchReason { get; set; }
    }
}
