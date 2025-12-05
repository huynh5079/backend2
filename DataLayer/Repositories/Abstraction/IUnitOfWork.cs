using DataLayer.Entities;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType.Abstraction;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }
        IRoleRepository Roles { get; }
        IStudentProfileRepository StudentProfiles { get; }
        IParentProfileRepository ParentProfiles { get; }
        ITutorProfileRepository TutorProfiles { get; }
        IMediaRepository Media { get; }
        IClassRepository Classes { get; }
        IClassScheduleRepository ClassSchedules { get; }
        IAttendanceRepository Attendances { get; }
        IWalletRepository Wallets { get; }
        ITransactionRepository Transactions { get; }
        IEscrowRepository Escrows { get; }
        IFeedbackRepository Feedbacks { get; }
        INotificationRepository Notifications { get; }
        IReportRepository Reports { get; }
        ILessonRepository Lessons { get; }
        IClassAssignRepository ClassAssigns { get; }
        IPaymentRepository Payments { get; }
        IPaymentLogRepository PaymentLogs { get; }
        IRescheduleRequestRepository RescheduleRequests { get; }
        IMessageRepository Messages { get; }
        ICommissionRepository Commissions { get; }
        IFavoriteTutorRepository FavoriteTutors { get; }
        IConversationRepository Conversations { get; }
        ITutorDepositEscrowRepository TutorDepositEscrows { get; }
        ISystemSettingsRepository SystemSettings { get; }
        
        // Quiz Feature Repositories
        IQuizRepository Quizzes { get; }
        IQuizQuestionRepository QuizQuestions { get; }
        IStudentQuizAttemptRepository StudentQuizAttempts { get; }
        IStudentQuizAnswerRepository StudentQuizAnswers { get; }
        IClassRepository2 Classes2 { get; }

        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
