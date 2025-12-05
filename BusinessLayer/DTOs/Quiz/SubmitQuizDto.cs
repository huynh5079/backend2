namespace BusinessLayer.DTOs.Quiz
{
    public class SubmitQuizDto
    {
        public string QuizId { get; set; }
        public List<QuizAnswerDto> Answers { get; set; }
    }

    public class QuizAnswerDto
    {
        public string QuestionId { get; set; }
        public char? SelectedAnswer { get; set; } // A, B, C, D or null
    }
}
