using BusinessLayer.DTOs.Quiz;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Service
{
    public class QuizService : IQuizService
    {
        private readonly IUnitOfWork _uow;
        private readonly IQuizFileParserService _fileParser;

        public QuizService(IUnitOfWork uow, IQuizFileParserService fileParser)
        {
            _uow = uow;
            _fileParser = fileParser;
        }

        public async Task<string> CreateQuizFromFileAsync(string tutorUserId, UploadQuizFileDto dto, CancellationToken ct)
        {
            // 1. Verify lesson exists and tutor owns it
            var lesson = await _uow.Lessons.GetAsync(l => l.Id == dto.LessonId && l.DeletedAt == null);
            if (lesson == null)
                throw new KeyNotFoundException("Lesson not found");

            var cls = await _uow.Classes2.GetAsync(c => c.Id == lesson.ClassId && c.DeletedAt == null);
            if (cls == null)
                throw new KeyNotFoundException("Class not found");

            var tutorProfile = await _uow.TutorProfiles.GetAsync(t => t.UserId == tutorUserId && t.DeletedAt == null);
            if (tutorProfile == null || cls.TutorId != tutorProfile.Id)
                throw new UnauthorizedAccessException("You do not have permission to create quiz for this lesson");

            // 2. Parse file using Gemini
            var parsedQuiz = await _fileParser.ParseFileAsync(dto.QuizFile, ct);

            // 3. Create Quiz entity
            var quiz = new Quiz
            {
                LessonId = dto.LessonId,
                Title = parsedQuiz.Title,
                Description = parsedQuiz.Description,
                TimeLimit = parsedQuiz.TimeLimit,
                PassingScore = parsedQuiz.PassingScore,
                QuizType = dto.QuizType,
                MaxAttempts = dto.QuizType == DataLayer.Enum.QuizType.Practice ? 0 : dto.MaxAttempts,
                IsActive = true
            };

            await _uow.Quizzes.CreateAsync(quiz);

            // 4. Create questions
            int orderIndex = 1;
            foreach (var q in parsedQuiz.Questions)
            {
                var question = new QuizQuestion
                {
                    QuizId = quiz.Id,
                    QuestionText = q.QuestionText,
                    OrderIndex = orderIndex++,
                    Points = 1, // Default 1 point per question
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation = q.Explanation
                };
                await _uow.QuizQuestions.CreateAsync(question);
            }

            await _uow.SaveChangesAsync();
            return quiz.Id;
        }

        public async Task<bool> DeleteQuizAsync(string tutorUserId, string quizId)
        {
            var quiz = await _uow.Quizzes.GetAsync(
                q => q.Id == quizId && q.DeletedAt == null,
                q => q.Include(x => x.Lesson).ThenInclude(l => l.Class));

            if (quiz == null)
                return false;

            var tutorProfile = await _uow.TutorProfiles.GetAsync(t => t.UserId == tutorUserId && t.DeletedAt == null);
            if (tutorProfile == null || quiz.Lesson.Class.TutorId != tutorProfile.Id)
                throw new UnauthorizedAccessException("You do not have permission to delete this quiz");

            quiz.DeletedAt = DateTime.Now;
            await _uow.Quizzes.UpdateAsync(quiz);
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<TutorQuizDto> GetQuizByIdAsync(string tutorUserId, string quizId)
        {
            // Get quiz with questions
            var quiz = await _uow.Quizzes.GetAsync(
                q => q.Id == quizId && q.DeletedAt == null,
                q => q.Include(x => x.Questions).Include(x => x.Lesson).ThenInclude(l => l.Class));

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found");

            // Verify tutor owns this quiz
            var tutorProfile = await _uow.TutorProfiles.GetAsync(t => t.UserId == tutorUserId && t.DeletedAt == null);
            if (tutorProfile == null || quiz.Lesson.Class.TutorId != tutorProfile.Id)
                throw new UnauthorizedAccessException("You do not have permission to view this quiz");

            // Return quiz with correct answers (for tutor)
            return new TutorQuizDto
            {
                Id = quiz.Id,
                LessonId = quiz.LessonId,
                Title = quiz.Title,
                Description = quiz.Description,
                TimeLimit = quiz.TimeLimit,
                PassingScore = quiz.PassingScore,
                IsActive = quiz.IsActive,
                QuizType = quiz.QuizType,
                MaxAttempts = quiz.MaxAttempts,
                TotalQuestions = quiz.Questions.Count(q => q.DeletedAt == null),
                CreatedAt = quiz.CreatedAt,
                Questions = quiz.Questions
                    .Where(q => q.DeletedAt == null)
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new TutorQuizQuestionDto
                    {
                        Id = q.Id,
                        QuestionText = q.QuestionText,
                        OrderIndex = q.OrderIndex,
                        Points = q.Points,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectAnswer = q.CorrectAnswer,
                        Explanation = q.Explanation
                    }).ToList()
            };
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetQuizzesByLessonAsync(string userId, string lessonId)
        {
            // Get lesson
            var lesson = await _uow.Lessons.GetAsync(l => l.Id == lessonId && l.DeletedAt == null,
                l => l.Include(x => x.Class));

            if (lesson == null)
                throw new KeyNotFoundException("Lesson not found");

            // Check if user has access (tutor owns class OR student enrolled)
            var tutorProfile = await _uow.TutorProfiles.GetAsync(t => t.UserId == userId && t.DeletedAt == null);
            var studentProfile = await _uow.StudentProfiles.GetAsync(s => s.UserId == userId && s.DeletedAt == null);

            bool hasAccess = false;
            
            if (tutorProfile != null && lesson.Class.TutorId == tutorProfile.Id)
            {
                hasAccess = true; // Tutor owns class
            }
            else if (studentProfile != null)
            {
                // Check if student is enrolled
                var classAssign = await _uow.ClassAssigns.GetAsync(
                    ca => ca.ClassId == lesson.ClassId &&
                          ca.StudentId == studentProfile.Id &&
                          ca.ApprovalStatus == DataLayer.Enum.ApprovalStatus.Approved &&
                          ca.DeletedAt == null);
                hasAccess = classAssign != null;
            }

            if (!hasAccess)
                throw new UnauthorizedAccessException("You do not have permission to view quizzes for this lesson");

            // Get all quizzes for this lesson
            var quizzes = await _uow.Quizzes.GetAllAsync(
                q => q.LessonId == lessonId && q.DeletedAt == null,
                q => q.Include(x => x.Questions));

            return quizzes.Select(q => new QuizSummaryDto
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                TotalQuestions = q.Questions.Count(qu => qu.DeletedAt == null),
                TimeLimit = q.TimeLimit,
                PassingScore = q.PassingScore,
                QuizType = q.QuizType,
                MaxAttempts = q.MaxAttempts,
                IsActive = q.IsActive,
                CreatedAt = q.CreatedAt
            }).OrderByDescending(q => q.CreatedAt).ToList();
        }

        public async Task<StudentQuizDto> StartQuizAsync(string studentUserId, string quizId)
        {
            // 1. Get quiz with questions
            var quiz = await _uow.Quizzes.GetAsync(
                q => q.Id == quizId && q.DeletedAt == null && q.IsActive,
                q => q.Include(x => x.Questions).Include(x => x.Lesson).ThenInclude(l => l.Class));

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found or not active");

            // 2. Verify student is enrolled
            var studentProfile = await _uow.StudentProfiles.GetAsync(s => s.UserId == studentUserId && s.DeletedAt == null);
            if (studentProfile == null)
                throw new UnauthorizedAccessException("Student profile not found");

            var classAssign = await _uow.ClassAssigns.GetAsync(
                ca => ca.ClassId == quiz.Lesson.ClassId && 
                      ca.StudentId == studentProfile.Id && 
                      ca.ApprovalStatus == DataLayer.Enum.ApprovalStatus.Approved &&
                      ca.DeletedAt == null);
            if (classAssign == null)
                throw new UnauthorizedAccessException("You are not enrolled in this class");

            // 3. Check attempt limits
            var attempts = await _uow.StudentQuizAttempts.GetAllAsync(
                a => a.QuizId == quizId && a.StudentProfileId == studentProfile.Id && a.DeletedAt == null);
            var attemptCount = attempts.Count();

            if (quiz.QuizType == DataLayer.Enum.QuizType.Test && quiz.MaxAttempts > 0 && attemptCount >= quiz.MaxAttempts)
                throw new InvalidOperationException($"You have reached the maximum number of attempts ({quiz.MaxAttempts}) for this quiz");

            // 4. Return quiz for student (without correct answers)
            return new StudentQuizDto
            {
                Id = quiz.Id,
                Title = quiz.Title,
                Description = quiz.Description,
                TimeLimit = quiz.TimeLimit,
                PassingScore = quiz.PassingScore,
                TotalQuestions = quiz.Questions.Count,
                QuizType = quiz.QuizType,
                MaxAttempts = quiz.MaxAttempts,
                CurrentAttemptCount = attemptCount,
                Questions = quiz.Questions.Select(q => new StudentQuizQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    OrderIndex = q.OrderIndex,
                    Points = q.Points,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD
                }).ToList()
            };
        }

        public async Task<QuizResultDto> SubmitQuizAsync(string studentUserId, SubmitQuizDto dto)
        {
            // 1. Get quiz and questions
            var quiz = await _uow.Quizzes.GetAsync(
                q => q.Id == dto.QuizId && q.DeletedAt == null,
                q => q.Include(x => x.Questions));

            if (quiz == null)
                throw new KeyNotFoundException("Quiz not found");

            var studentProfile = await _uow.StudentProfiles.GetAsync(s => s.UserId == studentUserId && s.DeletedAt == null);
            if (studentProfile == null)
                throw new UnauthorizedAccessException("Student profile not found");

            // 2. Create attempt
            var attempt = new StudentQuizAttempt
            {
                QuizId = dto.QuizId,
                StudentProfileId = studentProfile.Id,
                StartedAt = DateTime.Now,
                SubmittedAt = DateTime.Now,
                IsCompleted = true,
                TotalQuestions = quiz.Questions.Count(q => q.DeletedAt == null)
            };

            await _uow.StudentQuizAttempts.CreateAsync(attempt);

            // 3. Process answers and calculate score
            int correctCount = 0;
            var answerResults = new List<QuizAnswerResultDto>();

            var activeQuestions = quiz.Questions.Where(q => q.DeletedAt == null).ToList();
            foreach (var question in activeQuestions)
            {
                var submittedAnswer = dto.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
                char? selectedAnswer = submittedAnswer?.SelectedAnswer;
                bool isCorrect = selectedAnswer.HasValue && 
                                char.ToUpper(selectedAnswer.Value) == char.ToUpper(question.CorrectAnswer);

                if (isCorrect)
                    correctCount++;

                // Save student answer
                var studentAnswer = new StudentQuizAnswer
                {
                    AttemptId = attempt.Id,
                    QuestionId = question.Id,
                    SelectedAnswer = selectedAnswer,
                    IsCorrect = isCorrect
                };
                await _uow.StudentQuizAnswers.CreateAsync(studentAnswer);

                // Prepare result
                answerResults.Add(new QuizAnswerResultDto
                {
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    SelectedAnswer = selectedAnswer,
                    CorrectAnswer = question.CorrectAnswer,
                    IsCorrect = isCorrect,
                    Explanation = question.Explanation
                });
            }

            // 4. Update attempt with score
            attempt.CorrectAnswers = correctCount;
            attempt.ScorePercentage = quiz.Questions.Count > 0 
                ? Math.Round((decimal)correctCount / quiz.Questions.Count * 100, 2) 
                : 0;
            attempt.IsPassed = attempt.ScorePercentage >= quiz.PassingScore;

            await _uow.StudentQuizAttempts.UpdateAsync(attempt);
            await _uow.SaveChangesAsync();

            // 5. Return result
            return new QuizResultDto
            {
                AttemptId = attempt.Id,
                TotalQuestions = attempt.TotalQuestions,
                CorrectAnswers = attempt.CorrectAnswers,
                ScorePercentage = attempt.ScorePercentage,
                IsPassed = attempt.IsPassed,
                SubmittedAt = attempt.SubmittedAt.Value,
                AnswerDetails = answerResults
            };
        }

        public async Task<IEnumerable<QuizResultDto>> GetMyAttemptsAsync(string studentUserId, string quizId)
        {
            var studentProfile = await _uow.StudentProfiles.GetAsync(s => s.UserId == studentUserId && s.DeletedAt == null);
            if (studentProfile == null)
                throw new UnauthorizedAccessException("Student profile not found");

            var attempts = await _uow.StudentQuizAttempts.GetAllAsync(
                a => a.QuizId == quizId && 
                    a.StudentProfileId == studentProfile.Id && 
                    a.IsCompleted && 
                    a.DeletedAt == null,
                q => q.Include(a => a.Answers).ThenInclude(ans => ans.Question));

            return attempts.Select(a => new QuizResultDto
            {
                AttemptId = a.Id,
                TotalQuestions = a.TotalQuestions,
                CorrectAnswers = a.CorrectAnswers,
                ScorePercentage = a.ScorePercentage,
                IsPassed = a.IsPassed,
                SubmittedAt = a.SubmittedAt ?? a.CreatedAt,
                AnswerDetails = a.Answers.Select(ans => new QuizAnswerResultDto
                {
                    QuestionId = ans.QuestionId,
                    QuestionText = ans.Question.QuestionText,
                    SelectedAnswer = ans.SelectedAnswer,
                    CorrectAnswer = ans.Question.CorrectAnswer,
                    IsCorrect = ans.IsCorrect,
                    Explanation = ans.Question.Explanation
                }).ToList()
            }).ToList();
        }
    }
}
