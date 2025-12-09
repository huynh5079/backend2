using DataLayer.Enum;

namespace BusinessLayer.DTOs.Quiz
{
    // For student - no correct answers shown
    public class StudentQuizDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TimeLimit { get; set; }
        public int PassingScore { get; set; }
        public int TotalQuestions { get; set; }
        public QuizType QuizType { get; set; }
        public int MaxAttempts { get; set; }
        public int CurrentAttemptCount { get; set; } // Number of attempts already taken
        public List<StudentQuizQuestionDto> Questions { get; set; }
    }

    public class StudentQuizQuestionDto
    {
        public string Id { get; set; }
        public string QuestionText { get; set; }
        public string? ImageUrl { get; set; }
        public int OrderIndex { get; set; }
        public int Points { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        // No CorrectAnswer for students!
    }
}
