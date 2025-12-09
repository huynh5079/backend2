using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace DataLayer.Repositories
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly TpeduContext _ctx;

        public IUserRepository Users { get; }
        public IRoleRepository Roles { get; }
        public IStudentProfileRepository StudentProfiles { get; }
        public IParentProfileRepository ParentProfiles { get; }
        public ITutorProfileRepository TutorProfiles { get; }
        public IMediaRepository Media { get; }
        public IClassRepository Classes { get; }
        public IClassScheduleRepository ClassSchedules { get; }
        public IAttendanceRepository Attendances { get; }
        public IWalletRepository Wallets { get; }
        public ITransactionRepository Transactions { get; }
        public IEscrowRepository Escrows { get; }
        public IFeedbackRepository Feedbacks { get; }
        public INotificationRepository Notifications { get; }
        public IReportRepository Reports { get; }
        public ILessonRepository Lessons { get; }
        public IClassAssignRepository ClassAssigns { get; }
        public IPaymentRepository Payments { get; }
        public IPaymentLogRepository PaymentLogs { get; }
        public IRescheduleRequestRepository RescheduleRequests { get; }
        public IMessageRepository Messages { get; }
        public ICommissionRepository Commissions { get; }
        public IFavoriteTutorRepository FavoriteTutors { get; }
        public IConversationRepository Conversations { get; }
        public ITutorDepositEscrowRepository TutorDepositEscrows { get; }
        public ISystemSettingsRepository SystemSettings { get; }
        
        // Quiz Feature Repositories
        public IQuizRepository Quizzes { get; }
        public IQuizQuestionRepository QuizQuestions { get; }
        public IStudentQuizAttemptRepository StudentQuizAttempts { get; }
        public IStudentQuizAnswerRepository StudentQuizAnswers { get; }
        public IClassRepository2 Classes2 { get; }
        public IVideoAnalysisRepository VideoAnalyses { get; }

        public UnitOfWork(TpeduContext ctx,
                          IUserRepository users,
                          IRoleRepository roles,
                          IStudentProfileRepository studentProfiles,
                          IParentProfileRepository parentProfiles,
                          ITutorProfileRepository tutorProfiles,
                          IMediaRepository media,
                          IClassRepository classes,
                          IClassScheduleRepository classSchedules,
                          IAttendanceRepository attendances,
                          IWalletRepository wallets,
                          ITransactionRepository transactions,
                          IEscrowRepository escrows,
                          IFeedbackRepository feedbacks,
                          INotificationRepository notifications,
                          IReportRepository reports,
                          ILessonRepository lessons,
                          IClassAssignRepository classAssigns,
                          IPaymentRepository payments,
                          IPaymentLogRepository paymentLogs,
                          IRescheduleRequestRepository rescheduleRequests,
                          IMessageRepository messages,
                          ICommissionRepository commissions,
                          IFavoriteTutorRepository favoriteTutors,
                          IConversationRepository conversations,
                          ITutorDepositEscrowRepository tutorDepositEscrows,
                          ISystemSettingsRepository systemSettings,
                          IQuizRepository quizzes,
                          IQuizQuestionRepository quizQuestions,
                          IStudentQuizAttemptRepository studentQuizAttempts,
                          IStudentQuizAnswerRepository studentQuizAnswers,
                          IClassRepository2 classes2,
                          IVideoAnalysisRepository videoAnalyses)
        {
            _ctx = ctx;
            Users = users;
            Roles = roles;
            StudentProfiles = studentProfiles;
            ParentProfiles = parentProfiles;
            TutorProfiles = tutorProfiles;
            Media = media;
            Classes = classes;
            ClassSchedules = classSchedules;
            Attendances = attendances;
            Wallets = wallets;
            Transactions = transactions;
            Escrows = escrows;
            Feedbacks = feedbacks;
            Notifications = notifications;
            Reports = reports;
            Lessons = lessons;
            ClassAssigns = classAssigns;
            Payments = payments;
            PaymentLogs = paymentLogs;
            RescheduleRequests = rescheduleRequests;
            Messages = messages;
            Commissions = commissions;
            FavoriteTutors = favoriteTutors;
            Conversations = conversations;
            TutorDepositEscrows = tutorDepositEscrows;
            SystemSettings = systemSettings;
            Quizzes = quizzes;
            QuizQuestions = quizQuestions;
            StudentQuizAttempts = studentQuizAttempts;
            StudentQuizAnswers = studentQuizAnswers;
            Classes2 = classes2;
            VideoAnalyses = videoAnalyses;
        }

        public Task<int> SaveChangesAsync() => _ctx.SaveChangesAsync();

        public async Task<IDbContextTransaction> BeginTransactionAsync()
            => await _ctx.Database.BeginTransactionAsync();

        public void Dispose() => _ctx.Dispose();
    }
}
