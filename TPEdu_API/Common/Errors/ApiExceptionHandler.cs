using BusinessLayer.DTOs.API;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;

namespace TPEdu_API.Common.Errors
{
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ApiExceptionHandler> _logger;

        public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
            => _logger = logger;

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (status, message) = Map(exception);

            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

            httpContext.Response.StatusCode = status;
            httpContext.Response.ContentType = "application/json; charset=utf-8";

            var payload = JsonSerializer.Serialize(ApiResponse<object>.Fail(message));
            await httpContext.Response.WriteAsync(payload, cancellationToken);
            return true; // Đã handle xong
        }

        private static (int status, string message) Map(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ex.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ArgumentException or InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
                // Có thể map thêm DomainException, ValidationException nếu bạn có
                _ => (StatusCodes.Status500InternalServerError, "lỗi hệ thống")
            };
        }
    }
}
