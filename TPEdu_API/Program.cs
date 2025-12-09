using BusinessLayer.Options;
using BusinessLayer.Service;
using BusinessLayer.Service.Interface;
using BusinessLayer.Service.Interface.IScheduleService;
using BusinessLayer.Service.ScheduleService;
using BusinessLayer.Storage;
using DataLayer.Entities;
using DataLayer.Repositories;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.Abstraction.Schedule;
using DataLayer.Repositories.GenericType;
using DataLayer.Repositories.GenericType.Abstraction;
using DataLayer.Repositories.Schedule;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using System.Text.Json.Serialization;
using TPEdu_API.Common.Errors;
using TPEdu_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Allow API read string ("Monday", "Tuesday") 
        // Change to enum DayOfWeek (1, 2)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TPEdu API",
        Version = "v1"
    });
    
    // Cấu hình để Swagger hiểu IFormFile cho file uploads
    c.MapType<Microsoft.AspNetCore.Http.IFormFile>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // Với SignalR cần AllowCredentials, không thể dùng AllowAnyOrigin
        // Trong development, cho phép tất cả origins nhưng cần SetIsOriginAllowed
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Cần cho SignalR
    });
});

// DbContext
var sqlConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TpeduContext>(options =>
    options.UseSqlServer(sqlConnection));

/* // Optional: SQL retry pattern
builder.Services.AddDbContext<TpeduContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));
*/

// Generic Repository (open generic)
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<StoragePathResolver>();
builder.Services.AddScoped<IFileStorageService, CloudinaryStorageService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ITutorProfileApprovalService, TutorProfileApprovalService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminDirectoryService, AdminDirectoryService>();
builder.Services.AddScoped<ITutorProfileService, TutorProfileService>();
builder.Services.AddScoped<IPublicTutorService, PublicTutorService>();
builder.Services.AddScoped<IParentChildrenService, ParentChildrenService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IEscrowService, EscrowService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();
builder.Services.AddScoped<ICommissionManagementService, CommissionManagementService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IClassStatusCheckService, ClassStatusCheckService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<ILessonMaterialService, LessonMaterialService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IFavoriteTutorService, FavoriteTutorService>();
builder.Services.AddScoped<IQuizFileParserService, QuizFileParserService>();
builder.Services.AddScoped<IQuizContentValidatorService, QuizContentValidatorService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<CloudinaryStorageService>();

// Background Services
builder.Services.AddHostedService<ClassStatusCheckBackgroundService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationHubService, NotificationHubService>();
builder.Services.AddSingleton<TPEdu_API.Services.ChatConnectionManager>();
builder.Services.AddScoped<IChatHubService, TPEdu_API.Services.ChatHubService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMomoPaymentService, MomoPaymentService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IFavoriteTutorService, FavoriteTutorService>();
builder.Services.AddScoped<IQuizFileParserService, QuizFileParserService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IVideoAnalysisService, VideoAnalysisService>();
builder.Services.AddScoped<ILessonMaterialService, LessonMaterialService>();

// Schedule Transactions
builder.Services.AddScoped<IAvailabilityBlockService, AvailabilityBlockService>();
builder.Services.AddScoped<ITutorApplicationService, TutorApplicationService>();
builder.Services.AddScoped<IClassRequestService, ClassRequestService>();
builder.Services.AddScoped<IScheduleGenerationService, ScheduleGenerationService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IScheduleViewService, ScheduleViewService>();
builder.Services.AddScoped<IAssignService, AssignService>();
builder.Services.AddScoped<IStudentProfileService, StudentProfileService>();
builder.Services.AddScoped<ILessonRescheduleService, LessonRescheduleService>();
builder.Services.AddScoped<ILessonService, LessonService>();

// Exception Handler & ProblemDetails
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Repositories (specific)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IStudentProfileRepository, StudentProfileRepository>();
builder.Services.AddScoped<IParentProfileRepository, ParentProfileRepository>();
builder.Services.AddScoped<ITutorProfileRepository, TutorProfileRepository>();
builder.Services.AddScoped<IMediaRepository, MediaRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IClassScheduleRepository, ClassScheduleRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IEscrowRepository, EscrowRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentLogRepository, PaymentLogRepository>();
builder.Services.AddScoped<IRescheduleRequestRepository, RescheduleRequestRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<ICommissionRepository, CommissionRepository>();
builder.Services.AddScoped<IFavoriteTutorRepository, FavoriteTutorRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<ITutorDepositEscrowRepository, TutorDepositEscrowRepository>();
builder.Services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IQuizQuestionRepository, QuizQuestionRepository>();
builder.Services.AddScoped<IStudentQuizAttemptRepository, StudentQuizAttemptRepository>();
builder.Services.AddScoped<IStudentQuizAnswerRepository, StudentQuizAnswerRepository>();
builder.Services.AddScoped<DataLayer.Repositories.Abstraction.IVideoAnalysisRepository, DataLayer.Repositories.VideoAnalysisRepository>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Schedule Transactions
builder.Services.AddScoped<IClassRequestRepository, ClassRequestRepository>();
builder.Services.AddScoped<ITutorApplicationRepository, TutorApplicationRepository>();
builder.Services.AddScoped<IClassAssignRepository, ClassAssignRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();
builder.Services.AddScoped<IScheduleEntryRepository, ScheduleEntryRepository>();
builder.Services.AddScoped<IAvailabilityBlockRepository, AvailabilityBlockRepository>();
builder.Services.AddScoped<IScheduleUnitOfWork, ScheduleUnitOfWork>();
builder.Services.AddScoped<IClassRepository2, ClassRepository2>();


// Options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<SystemWalletOptions>(builder.Configuration.GetSection("SystemWallet"));
builder.Services.Configure<MomoOptions>(builder.Configuration.GetSection("Momo"));
builder.Services.Configure<CommissionOptions>(builder.Configuration.GetSection("Commission"));

builder.Services.AddHttpClient();

// Redis (Azure Cache for Redis)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrEmpty(redisConnectionString))
{
    // Đăng ký IConnectionMultiplexer (Dành cho OtpService và các service dùng Redis trực tiếp)
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString, true);
        configuration.AbortOnConnectFail = false; // Quan trọng cho Azure để tránh lỗi timeout lúc khởi động
        configuration.ConnectTimeout = 3000; // 3 seconds
        configuration.SyncTimeout = 3000;
        configuration.AsyncTimeout = 3000;
        return ConnectionMultiplexer.Connect(configuration);
    });

    // Đăng ký IDistributedCache nếu bạn muốn dùng cache của .NET
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "tpedu:";
    });
}
else
{
    // Fallback to in-memory cache nếu không có Redis config
    builder.Services.AddDistributedMemoryCache();
    
    // Tạo một mock IConnectionMultiplexer để tránh lỗi DI
    // Lưu ý: OtpService sẽ không hoạt động nếu không có Redis thực sự
    // Để chạy local không cần Redis, comment Redis connection string trong appsettings.Development.json
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        // Tạm thời tạo mock connection để tránh lỗi DI khi chạy local
        // Lưu ý: Các service dùng Redis (như OtpService) sẽ không hoạt động
        try
        {
            // Thử connect với abortConnect=false để không block nếu không có Redis
            return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false,connectTimeout=1000");
        }
        catch
        {
            // Nếu không connect được, vẫn trả về null để tránh crash
            // Service sẽ handle null check
            return null!;
        }
    });
}

// JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Cấu hình JWT cho SignalR WebSocket
        // SignalR sử dụng query string để truyền token khi handshake
        o.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Lấy token từ query string (SignalR WebSocket handshake)
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // Chỉ lấy token từ SignalR hub endpoint
                if (!string.IsNullOrEmpty(accessToken) && 
                    (path.StartsWithSegments("/tpedu/v1/notificationhub") || 
                     path.StartsWithSegments("/tpedu/v1/chathub")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub
app.MapHub<TPEdu_API.Hubs.NotificationHub>("/tpedu/v1/notificationhub");
app.MapHub<TPEdu_API.Hubs.ChatHub>("/tpedu/v1/chathub");

app.Run();
