using DataLayer.Enum;

namespace BusinessLayer.DTOs.Quiz
{
    // Summary info for listing quizzes
    public class QuizSummaryDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TotalQuestions { get; set; }
        public int TimeLimit { get; set; }
        public int PassingScore { get; set; }
        public QuizType QuizType { get; set; }
        public int MaxAttempts { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
