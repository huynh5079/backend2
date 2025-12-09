using Microsoft.AspNetCore.Http;

namespace BusinessLayer.DTOs.Quiz
{
    public class UpdateQuizQuestionDto
    {
        public string QuestionText { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public char CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public IFormFile? Image { get; set; } // Optional image upload
    }
}
