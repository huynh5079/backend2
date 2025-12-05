namespace BusinessLayer.DTOs.Quiz
{
    // Internal DTO after file parsing by Gemini
    public class ParsedQuizDto
    {
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TimeLimit { get; set; } // Minutes
        public int PassingScore { get; set; } // Percentage 0-100
        public List<ParsedQuizQuestionDto> Questions { get; set; }
    }

    public class ParsedQuizQuestionDto
    {
        public string QuestionText { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public char CorrectAnswer { get; set; } // 'A', 'B', 'C', 'D'
        public string? Explanation { get; set; }
    }
}
