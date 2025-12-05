using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class QuizQuestion : BaseEntity
{
    public string QuizId { get; set; }
    public string QuestionText { get; set; }
    public int OrderIndex { get; set; } // Thứ tự câu hỏi
    public int Points { get; set; } // Điểm của câu hỏi này (default = 1)
    
    // 4 lựa chọn
    public string OptionA { get; set; }
    public string OptionB { get; set; }
    public string OptionC { get; set; }
    public string OptionD { get; set; }
    
    public char CorrectAnswer { get; set; } // 'A', 'B', 'C', hoặc 'D'
    public string? Explanation { get; set; } // Giải thích đáp án (optional)
    
    // Navigation
    public virtual Quiz Quiz { get; set; }
    public virtual ICollection<StudentQuizAnswer> StudentAnswers { get; set; } = new List<StudentQuizAnswer>();
}
