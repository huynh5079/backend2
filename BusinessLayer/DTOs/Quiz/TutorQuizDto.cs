using DataLayer.Enum;

namespace BusinessLayer.DTOs.Quiz
{
    // For tutor - includes correct answers
    public class TutorQuizDto
    {
        public string Id { get; set; }
        public string LessonId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TimeLimit { get; set; }
        public int PassingScore { get; set; }
        public bool IsActive { get; set; }
        public QuizType QuizType { get; set; }
        public int MaxAttempts { get; set; }
        public int TotalQuestions { get; set; }
        public List<TutorQuizQuestionDto> Questions { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TutorQuizQuestionDto
    {
        public string Id { get; set; }
        public string QuestionText { get; set; }
        public int OrderIndex { get; set; }
        public int Points { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public char CorrectAnswer { get; set; } // Tutor can see correct answer
        public string? Explanation { get; set; }
    }
}
