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
builder.Services.AddSwaggerGen();

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
builder.Services.AddDbContext<TpeduContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationHubService, NotificationHubService>();
builder.Services.AddScoped<IChatHubService, TPEdu_API.Services.ChatHubService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMomoPaymentService, MomoPaymentService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<ILessonMaterialService, LessonMaterialService>();
builder.Services.AddScoped<IReportService, ReportService>();

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

builder.Services.AddHttpClient();

// Redis (Azure Cache for Redis)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConfig = builder.Configuration.GetSection("Redis")["Configuration"];
    if (string.IsNullOrWhiteSpace(redisConfig))
        throw new InvalidOperationException("Redis configuration string is missing");

    return ConnectionMultiplexer.Connect(redisConfig);
});

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
