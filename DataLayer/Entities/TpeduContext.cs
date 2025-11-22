using DataLayer.Enum;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities;

public partial class TpeduContext : DbContext
{
    public TpeduContext() { }
    public TpeduContext(DbContextOptions<TpeduContext> options) : base(options) { }

    public virtual DbSet<Attendance> Attendances { get; set; }
    public virtual DbSet<AvailabilityBlock> AvailabilityBlocks { get; set; }
    public virtual DbSet<Class> Classes { get; set; }
    public virtual DbSet<ClassAssign> ClassAssigns { get; set; }
    public virtual DbSet<ClassSchedule> ClassSchedules { get; set; }
    public virtual DbSet<ClassRequest> ClassRequests { get; set; }
    public virtual DbSet<ClassRequestSchedule> ClassRequestSchedules { get; set; }
    public virtual DbSet<Feedback> Feedbacks { get; set; }
    public virtual DbSet<LearningMaterial> LearningMaterials { get; set; }
    public virtual DbSet<Lesson> Lessons { get; set; }
    public virtual DbSet<Media> Media { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<ParentProfile> ParentProfiles { get; set; }
    public virtual DbSet<Report> Reports { get; set; }
    public virtual DbSet<RescheduleRequest> RescheduleRequests { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<ScheduleEntry> ScheduleEntries { get; set; }
    public virtual DbSet<StudentProfile> StudentProfiles { get; set; }
    public virtual DbSet<Transaction> Transactions { get; set; }
    public virtual DbSet<TutorApplication> TutorApplications { get; set; }
    public virtual DbSet<TutorProfile> TutorProfiles { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Wallet> Wallets { get; set; }
    public virtual DbSet<Escrow> Escrows { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<PaymentLog> PaymentLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("Attendance");

            entity.HasIndex(e => new { e.LessonId, e.StudentId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Attendance_Lesson_Student");

            entity.HasIndex(e => e.LessonId, "IX_Attendance_LessonID");
            entity.HasIndex(e => e.StudentId, "IX_Attendance_StudentID");

            entity.Property(e => e.LessonId).HasMaxLength(450).HasColumnName("LessonID");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.StudentId)
                .HasMaxLength(450)
                .HasColumnName("StudentID");

            entity.HasOne(d => d.Lesson).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Attendance_Lesson");

            entity.HasOne(d => d.Student).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Attendance_StudentProfile");
        });

        modelBuilder.Entity<AvailabilityBlock>(entity =>
        {
            entity.ToTable("AvailabilityBlock");

            entity.HasIndex(e => e.TutorId, "IX_AvailabilityBlock_TutorID");

            entity.Property(e => e.Title).HasMaxLength(1000);
            entity.Property(e => e.TutorId).HasMaxLength(450).HasColumnName("TutorID");

            entity.HasOne(d => d.Tutor).WithMany(p => p.AvailabilityBlocks)
                .HasForeignKey(d => d.TutorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AvailabilityBlock_TutorProfile");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Class__CB1927C08608731F");
            entity.ToTable("Class");

            entity.HasIndex(e => e.TutorId, "IX_Class_TutorId");

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EducationLevel).HasMaxLength(450);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.Mode).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Subject).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Tutor).WithMany(p => p.Classes)
                .HasForeignKey(d => d.TutorId)
                .HasConstraintName("FK__Class__TutorId__07C12930");
        });

        modelBuilder.Entity<ClassAssign>(entity =>
        {
            entity.ToTable("ClassAssign");

            entity.HasIndex(e => e.ClassId, "IX_ClassAssign_ClassId");
            entity.HasIndex(e => e.StudentId, "IX_ClassAssign_StudentId");

            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(50).HasConversion<string>().HasDefaultValue(ApprovalStatus.Pending);
            entity.Property(e => e.ClassId).HasMaxLength(450);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EnrolledAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50).HasConversion<string>().HasDefaultValue(PaymentStatus.Pending);
            entity.Property(e => e.StudentId).HasMaxLength(450);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassAssigns)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("FK_ClassAssign_Class");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassAssigns)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("FK_ClassAssign_StudentProfile");
        });

        modelBuilder.Entity<ClassSchedule>(entity =>
        {
            entity.ToTable("ClassSchedule");

            entity.HasIndex(e => e.ClassId, "IX_ClassSchedule_ClassID");
            entity.Property(e => e.ClassId).HasColumnName("ClassID");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassSchedules)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassSchedule_Class");
        });

        modelBuilder.Entity<ClassRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ClassReq__33A8517A858CC078");
            entity.ToTable("ClassRequest");

            entity.HasIndex(e => e.StudentId, "IX_ClassRequest_StudentId");
            entity.HasIndex(e => e.TutorId, "IX_ClassRequest_TutorId");

            entity.Property(e => e.Budget).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.EducationLevel).HasMaxLength(450);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.Mode).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.Subject).HasMaxLength(1000);

            entity.HasOne(d => d.Student).WithMany(p => p.ClassRequests)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("FK__ClassRequ__Stude__0A9D95DB");

            entity.HasOne(d => d.Tutor).WithMany(p => p.ClassRequests)
                .HasForeignKey(d => d.TutorId)
                .HasConstraintName("FK__ClassRequ__Tutor__0B91BA14");
        });

        modelBuilder.Entity<ClassRequestSchedule>(entity =>
        {
            entity.ToTable("ClassRequestSchedule");

            entity.HasIndex(e => e.ClassRequestId, "IX_ClassRequestSchedule_ClassRequestId");

            entity.HasOne(d => d.ClassRequest).WithMany(p => p.ClassRequestSchedules)
                .HasForeignKey(d => d.ClassRequestId)
                .HasConstraintName("FK_ClassRequestSchedule_ClassRequest");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Feedback__6A4BEDD6F1A4F2BB");
            entity.ToTable("Feedback");

            entity.HasIndex(e => new { e.FromUserId, e.ToUserId, e.ClassId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Feedback_From_To_Class");

            entity.HasIndex(e => new { e.FromUserId, e.ToUserId, e.LessonId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Feedback_From_To_Lesson");

            entity.HasIndex(e => e.FromUserId, "IX_Feedback_FromUserId");
            entity.HasIndex(e => e.LessonId, "IX_Feedback_LessonId");
            entity.HasIndex(e => e.ClassId, "IX_Feedback_ClassId");
            entity.HasIndex(e => e.ToUserId, "IX_Feedback_ToUserId");

            entity.Property(e => e.IsPublicOnTutorProfile).HasDefaultValue(false);
            entity.Property(e => e.Comment).HasMaxLength(1000);

            entity.HasOne(d => d.FromUser).WithMany(p => p.FeedbackFromUsers)
                .HasForeignKey(d => d.FromUserId)
                .HasConstraintName("FK__Feedback__FromUs__1332DBDC");

            entity.HasOne(d => d.Lesson).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.LessonId)
                .HasConstraintName("FK__Feedback__Lesson__151B244E");

            entity.HasOne(d => d.Class).WithMany(p => p.Feedbacks)
                  .HasForeignKey(d => d.ClassId)
                  .HasConstraintName("FK__Feedback__Class__261B255A");

            entity.HasOne(d => d.ToUser).WithMany(p => p.FeedbackToUsers)
                .HasForeignKey(d => d.ToUserId)
                .HasConstraintName("FK__Feedback__ToUser__14270015");
        });

        modelBuilder.Entity<LearningMaterial>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Learning__C50610F7D00FA71A");
            entity.ToTable("LearningMaterial");

            entity.HasIndex(e => e.ClassId, "IX_LearningMaterial_ClassId");

            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(1000);
            entity.Property(e => e.Type).HasMaxLength(500);

            entity.HasOne(d => d.Class).WithMany(p => p.LearningMaterials)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("FK__LearningM__Class__123EB7A3");
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.ToTable("Lesson");

            entity.HasIndex(e => e.ClassId, "IX_Lesson_ClassId");

            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(1000);

            entity.HasOne(d => d.Class).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("FK_Lesson_Class");
        });

        modelBuilder.Entity<Media>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Media__B2C2B5CF88789472");

            entity.HasIndex(e => e.LessonId, "IX_Media_LessonId");
            entity.HasIndex(e => e.OwnerUserId, "IX_Media_OwnerUserId");
            entity.HasIndex(e => e.TutorProfileId, "IX_Media_TutorProfileId");

            entity.Property(e => e.FileUrl).HasMaxLength(1024);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.MediaType).HasMaxLength(255);
            entity.Property(e => e.Context).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.ProviderPublicId).HasMaxLength(255);

            entity.HasOne(d => d.OwnerUser)
                .WithMany()
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Lesson).WithMany(p => p.Media)
                .HasForeignKey(d => d.LessonId)
                .IsRequired(false)
                .HasConstraintName("FK__Media__LessonId__114A936A");

            entity.HasOne(d => d.TutorProfile)
                .WithMany(p => p.Certificates)
                .HasForeignKey(d => d.TutorProfileId)
                .IsRequired(false);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Message__C87C0C9C8E34634D");
            entity.ToTable("Message");

            entity.HasIndex(e => e.ReceiverId, "IX_Message_ReceiverId");
            entity.HasIndex(e => e.SenderId, "IX_Message_SenderId");

            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(255);

            entity.HasOne(d => d.Receiver).WithMany(p => p.MessageReceivers)
                .HasForeignKey(d => d.ReceiverId)
                .HasConstraintName("FK__Message__Receive__19DFD96B");

            entity.HasOne(d => d.Sender).WithMany(p => p.MessageSenders)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("FK__Message__SenderI__18EBB532");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__20CF2E120CBDEB29");
            entity.ToTable("Notification");

            entity.HasIndex(e => e.UserId, "IX_Notification_UserId");

            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(1000);

            // Add explicit enum→string conversions and ensure columns exist
            entity.Property(e => e.Type).HasMaxLength(255).HasConversion<string>();
            entity.Property(e => e.Status).HasMaxLength(255).HasConversion<string>();

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Notificat__UserI__1AD3FDA4");
        });

        modelBuilder.Entity<ParentProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ParentPr__D339516FB6AE405F");
            entity.ToTable("ParentProfile");

            entity.HasIndex(e => e.LinkedStudentId, "IX_ParentProfile_LinkedStudentId");
            entity.HasIndex(e => e.UserId, "IX_ParentProfile_UserId");

            entity.Property(e => e.Relationship).HasMaxLength(500);

            entity.HasOne(d => d.LinkedStudent).WithMany(p => p.ParentProfiles)
                .HasForeignKey(d => d.LinkedStudentId)
                .HasConstraintName("FK__ParentPro__Linke__05D8E0BE");

            entity.HasOne(d => d.User).WithMany(p => p.ParentProfiles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__ParentPro__UserI__04E4BC85");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Report__D5BD48055AA94599");
            entity.ToTable("Report");

            entity.HasIndex(e => e.ReporterId, "IX_Report_ReporterId");
            entity.HasIndex(e => e.TargetLessonId, "IX_Report_TargetLessonId");
            entity.HasIndex(e => e.TargetUserId, "IX_Report_TargetUserId");
            entity.HasIndex(e => e.TargetMediaId, "IX_Report_TargetMediaId");

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(255).HasConversion<string>();

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .HasConstraintName("FK__Report__Reporter__160F4887");

            entity.HasOne(d => d.TargetLesson).WithMany(p => p.Reports)
                .HasForeignKey(d => d.TargetLessonId)
                .HasConstraintName("FK__Report__TargetLe__17F790F9");

            entity.HasOne(d => d.TargetUser).WithMany(p => p.ReportTargetUsers)
                .HasForeignKey(d => d.TargetUserId)
                .HasConstraintName("FK__Report__TargetUs__17036CC0");

            entity.HasOne(d => d.TargetMedia)
                  .WithMany()
                  .HasForeignKey(d => d.TargetMediaId)
                  .HasConstraintName("FK_Report_TargetMedia");
        });

        modelBuilder.Entity<RescheduleRequest>(entity =>
        {
            entity.ToTable("RescheduleRequest");

            entity.HasIndex(e => e.LessonId);
            entity.HasIndex(e => e.RequesterUserId);
            entity.HasIndex(e => e.ResponderUserId);
            entity.HasIndex(e => e.OriginalScheduleEntryId);

            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.RequesterUser)
                .WithMany()
                .HasForeignKey(d => d.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ResponderUser)
                .WithMany()
                .HasForeignKey(d => d.ResponderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Lesson)
                .WithMany() // Một Lesson có thể có nhiều yêu cầu đổi lịch
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.OriginalScheduleEntry)
                .WithMany() // Một ScheduleEntry có thể bị yêu cầu đổi nhiều lần
                .HasForeignKey(d => d.OriginalScheduleEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Role__8AFACE1A59C340B0");
            entity.ToTable("Role");

            entity.Property(e => e.RoleName).HasConversion<string>().HasMaxLength(255);
        });

        modelBuilder.Entity<ScheduleEntry>(entity =>
        {
            entity.ToTable("ScheduleEntry");

            entity.HasIndex(e => new { e.TutorId, e.StartTime, e.EndTime })
                  .HasDatabaseName("IX_ScheduleEntry_Tutor_TimeRange");

            entity.HasIndex(e => e.BlockId, "IX_ScheduleEntry_BlockID");
            entity.HasIndex(e => e.LessonId, "IX_ScheduleEntry_LessonID");
            entity.HasIndex(e => new { e.TutorId, e.StartTime }, "UQ_ScheduleEntry_Tutor_StartTime").IsUnique();

            entity.Property(e => e.BlockId).HasMaxLength(450).HasColumnName("BlockID");
            entity.Property(e => e.EntryType).HasConversion<string>().HasMaxLength(500);
            entity.Property(e => e.LessonId).HasMaxLength(450).HasColumnName("LessonID");
            entity.Property(e => e.TutorId).HasColumnName("TutorID");

            entity.HasOne(d => d.Block).WithMany(p => p.ScheduleEntries)
                .HasForeignKey(d => d.BlockId)
                .HasConstraintName("FK_ScheduleEntry_AvailabilityBlock");

            entity.HasOne(d => d.Lesson).WithMany(p => p.ScheduleEntries)
                .HasForeignKey(d => d.LessonId)
                .HasConstraintName("FK_ScheduleEntry_Lesson");

            entity.HasOne(d => d.Tutor).WithMany(p => p.ScheduleEntries)
                .HasForeignKey(d => d.TutorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ScheduleEntry_TutorProfile");
        });

        modelBuilder.Entity<StudentProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StudentP__32C52B9926DD24AA");
            entity.ToTable("StudentProfile");

            entity.HasIndex(e => e.UserId, "IX_StudentProfile_UserId");

            entity.Property(e => e.EducationLevel).HasMaxLength(450);
            entity.Property(e => e.PreferredSubjects).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithMany(p => p.StudentProfiles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__StudentPr__UserI__02FC7413");
        });

      
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Transact__55433A6B");
            entity.ToTable("Transaction");

            entity.HasIndex(e => e.WalletId, "IX_Transaction_WalletId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");   
            entity.Property(e => e.Status).HasMaxLength(32).HasConversion<string>();                 
            entity.Property(e => e.Type).HasMaxLength(32).HasConversion<string>();                   
            entity.Property(e => e.Note).HasMaxLength(256);                  
            entity.Property(e => e.CounterpartyUserId).HasMaxLength(450);    

            entity.HasOne(d => d.Wallet).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("FK_Transaction_Wallet");
        });
        
        modelBuilder.Entity<Escrow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Escrow__Id");
            entity.ToTable("Escrow");
            entity.Property(e => e.GrossAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CommissionRate).HasColumnType("decimal(5,2)");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.ClassId).HasMaxLength(450);
            entity.Property(e => e.PayerUserId).HasMaxLength(450);
            entity.Property(e => e.TutorUserId)
                  .HasMaxLength(450)
                  .IsRequired(false);

            entity.HasIndex(e => e.TutorUserId);
            entity.HasOne(e => e.TutorUser)
                  .WithMany()
                  .HasForeignKey(e => e.TutorUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ClassId);
            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PayerUser)
                  .WithMany()
                  .HasForeignKey(e => e.PayerUserId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Escrow_PayerUser");

            entity.HasOne(e => e.TutorUser)
                  .WithMany()
                  .HasForeignKey(e => e.TutorUserId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("FK_Escrow_TutorUser");
        });
        
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payment");

            entity.HasIndex(e => new { e.Provider, e.OrderId })
                .IsUnique()
                .HasDatabaseName("UQ_Payment_Provider_OrderId");

            entity.Property(e => e.Provider)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasDefaultValue(PaymentStatus.Pending);

            entity.Property(e => e.ContextType)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.OrderId)
                .HasMaxLength(255);

            entity.Property(e => e.ContextId)
                .HasMaxLength(450);

            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND");

            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 0)");

            entity.Property(e => e.Message)
                .HasMaxLength(1000);

            entity.Property(e => e.ExtraData)
                .HasMaxLength(2000);

            entity.Property(e => e.RequestId)
                .HasMaxLength(255);

            entity.Property(e => e.TransactionId)
                .HasMaxLength(255);

            entity.HasMany(e => e.Logs)
                .WithOne(l => l.Payment)
                .HasForeignKey(l => l.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentLog>(entity =>
        {
            entity.ToTable("PaymentLog");

            entity.HasIndex(e => e.PaymentId)
                .HasDatabaseName("IX_PaymentLog_PaymentId");

            entity.Property(e => e.PaymentId)
                .HasMaxLength(450);

            entity.Property(e => e.Event)
                .HasMaxLength(255);

            entity.Property(e => e.Payload)
                .HasColumnType("nvarchar(max)");
        });


        modelBuilder.Entity<TutorApplication>(entity =>
        {
            //entity.HasKey(e => e.ApplicationId).HasName("PK__TutorApp__C93A4C990407478F");
            entity.ToTable("TutorApplication");

            entity.Property(e => e.AppliedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ClassRequestId).HasMaxLength(450);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.TutorId).HasMaxLength(450);

            entity.HasOne(d => d.ClassRequest).WithMany(p => p.TutorApplications)
                .HasForeignKey(d => d.ClassRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_Request");

            entity.HasOne(d => d.Tutor).WithMany(p => p.TutorApplications)
                .HasForeignKey(d => d.TutorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Application_Tutor");
        });

        modelBuilder.Entity<TutorProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TutorPro__77C70FE2F53C4D03");
            entity.ToTable("TutorProfile");

            entity.HasIndex(e => e.UserId, "IX_TutorProfile_UserId");

            entity.Property(e => e.Bio).HasMaxLength(1000);
            entity.Property(e => e.ExperienceDetails).HasMaxLength(1000);
            entity.Property(e => e.ReviewStatus).HasConversion<string>().HasMaxLength(255);

            entity.HasOne(d => d.User).WithMany(p => p.TutorProfiles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__TutorProf__UserI__06CD04F7");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__User__1788CC4C79B935BF");
            entity.ToTable("User");

            entity.HasIndex(e => e.RoleId, "IX_User_RoleId");
            entity.HasIndex(e => e.Email, "UQ__User__A9D105343AB23C97").IsUnique().HasFilter("([Email] IS NOT NULL)");

            entity.Property(e => e.Address).HasMaxLength(1000);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.UserName).HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(255);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(255);
            entity.Property(e => e.Gender).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.DateOfBirth).HasColumnType("date");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__User__RoleId__02084FDA");
        });

        
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Wallet__84D4F90E");
            entity.ToTable("Wallet");

            
            entity.HasIndex(e => e.UserId, "UQ_Wallet_UserId").IsUnique();

          
            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("VND");
            entity.Property(e => e.IsFrozen).HasDefaultValue(false);

           
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(d => d.User).WithMany(p => p.Wallets)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Wallet_User");
        });
        

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
