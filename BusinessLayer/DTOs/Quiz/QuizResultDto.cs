using DataLayer.Enum;

namespace BusinessLayer.DTOs.Quiz
{
    public class QuizResultDto
    {
        public string AttemptId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public decimal ScorePercentage { get; set; }
        public bool IsPassed { get; set; }
        public DateTime SubmittedAt { get; set; }
        public List<QuizAnswerResultDto> AnswerDetails { get; set; }
    }

    public class QuizAnswerResultDto
    {
        public string QuestionId { get; set; }
        public string QuestionText { get; set; }
        public char? SelectedAnswer { get; set; }
        public char CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
    }
}
