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
                          IMessageRepository messages)
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
        }

        public Task<int> SaveChangesAsync() => _ctx.SaveChangesAsync();

        public async Task<IDbContextTransaction> BeginTransactionAsync()
            => await _ctx.Database.BeginTransactionAsync();

        public void Dispose() => _ctx.Dispose();
    }
}
