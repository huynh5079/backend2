using DataLayer.Enum;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class Quiz : BaseEntity
{
    public string LessonId { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public int TimeLimit { get; set; } // Số phút, 0 = không giới hạn
    public int PassingScore { get; set; } // % điểm để pass (0-100)
    public bool IsActive { get; set; } // Tutor có thể tắt/mở quiz
    
    // Quiz Type and Attempt Limits
    public QuizType QuizType { get; set; } // Practice or Test
    public int MaxAttempts { get; set; } // 0 = unlimited (for Practice)
    
    // Navigation
    public virtual Lesson Lesson { get; set; }
    public virtual ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public virtual ICollection<StudentQuizAttempt> Attempts { get; set; } = new List<StudentQuizAttempt>();
}
