using BusinessLayer.DTOs.Quiz;

namespace BusinessLayer.Service.Interface
{
    public interface IQuizService
    {
        // Tutor endpoints
        Task<string> CreateQuizFromFileAsync(string tutorUserId, UploadQuizFileDto dto, CancellationToken ct);
        Task<bool> DeleteQuizAsync(string tutorUserId, string quizId);
        Task<bool> UpdateQuizQuestionAsync(string tutorUserId, string questionId, UpdateQuizQuestionDto dto);
        Task<TutorQuizDto> GetQuizByIdAsync(string tutorUserId, string quizId);
        Task<IEnumerable<QuizSummaryDto>> GetQuizzesByLessonAsync(string userId, string lessonId);
        
        // Student endpoints  
        Task<StudentQuizDto> StartQuizAsync(string studentUserId, string quizId);
        Task<QuizResultDto> SubmitQuizAsync(string studentUserId, SubmitQuizDto dto);
        Task<IEnumerable<QuizResultDto>> GetMyAttemptsAsync(string studentUserId, string quizId);
        
        // Parent endpoints
        Task<IEnumerable<QuizResultDto>> GetStudentAttemptsForParentAsync(string parentUserId, string studentProfileId, string quizId);
    }
}
