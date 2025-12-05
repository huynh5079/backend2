using System;

namespace DataLayer.Entities;

public partial class StudentQuizAnswer : BaseEntity
{
    public string AttemptId { get; set; }
    public string QuestionId { get; set; }
    public char? SelectedAnswer { get; set; } // 'A', 'B', 'C', 'D', hoặc null nếu bỏ qua
    public bool IsCorrect { get; set; }
    
    // Navigation
    public virtual StudentQuizAttempt Attempt { get; set; }
    public virtual QuizQuestion Question { get; set; }
}
