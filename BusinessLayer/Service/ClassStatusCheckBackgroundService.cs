using BusinessLayer.Service.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Service
{
    /// <summary>
    /// Background service để tự động kiểm tra và cập nhật trạng thái lớp học
    /// Chạy định kỳ mỗi 5 phút
    /// </summary>
    public class ClassStatusCheckBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ClassStatusCheckBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Chạy mỗi 5 phút

        public ClassStatusCheckBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ClassStatusCheckBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ClassStatusCheckBackgroundService đã bắt đầu");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo scope để inject scoped services
                    using var scope = _serviceProvider.CreateScope();
                    var classStatusCheckService = scope.ServiceProvider.GetRequiredService<IClassStatusCheckService>();

                    _logger.LogInformation("Bắt đầu kiểm tra trạng thái lớp học...");
                    
                    var updatedCount = await classStatusCheckService.CheckAndUpdateClassStatusAsync(stoppingToken);
                    
                    if (updatedCount > 0)
                    {
                        _logger.LogInformation("Đã cập nhật {Count} lớp sang trạng thái Ongoing", updatedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi chạy background job kiểm tra trạng thái lớp học");
                }

                // Đợi 5 phút trước khi chạy lại
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("ClassStatusCheckBackgroundService đã dừng");
        }
    }
}

