using DataLayer.Enum;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.DTOs.Quiz
{
    public class UploadQuizFileDto
    {
        public string LessonId { get; set; }
        public IFormFile QuizFile { get; set; } // .txt or .docx file
        public QuizType QuizType { get; set; } // Practice or Test
        public int MaxAttempts { get; set; } // Only for Test type (0 = unlimited for Practice)
    }
}
