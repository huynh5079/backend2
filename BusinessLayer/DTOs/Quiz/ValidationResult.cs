using DataLayer.Enum;

namespace BusinessLayer.DTOs.Quiz
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public ValidationIssue? Issue { get; set; }
    }
}
