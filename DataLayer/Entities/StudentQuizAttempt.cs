using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class StudentQuizAttempt : BaseEntity
{
    public string QuizId { get; set; }
    public string StudentProfileId { get; set; }
    
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsCompleted { get; set; }
    
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public decimal ScorePercentage { get; set; } // 0-100
    public bool IsPassed { get; set; }
    
    // Navigation
    public virtual Quiz Quiz { get; set; }
    public virtual StudentProfile Student { get; set; }
    public virtual ICollection<StudentQuizAnswer> Answers { get; set; } = new List<StudentQuizAnswer>();
}
