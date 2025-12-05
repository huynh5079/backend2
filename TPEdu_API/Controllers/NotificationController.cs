using BusinessLayer.DTOs.Notification;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;
using System.Linq;
using System.Threading.Tasks;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/notifications")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly IUnitOfWork _uow;

        public NotificationController(IUnitOfWork uow)
        {
            _uow = uow;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyNotifications(
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 50,
            [FromQuery] bool? unreadOnly = null)
        {
            var userId = User.RequireUserId();
            
            var filter = unreadOnly == true 
                ? (System.Linq.Expressions.Expression<System.Func<DataLayer.Entities.Notification, bool>>?)(n => n.UserId == userId && n.Status == NotificationStatus.Unread)
                : (System.Linq.Expressions.Expression<System.Func<DataLayer.Entities.Notification, bool>>?)(n => n.UserId == userId);

            var notifications = await _uow.Notifications.GetAllAsync(filter);
            var total = notifications.Count();
            var items = notifications
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type.ToString(),
                    Status = n.Status.ToString(),
                    RelatedEntityId = n.RelatedEntityId,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt
                });

            return Ok(new { items, page = pageNumber, size = pageSize, total });
        }

        [HttpGet("me/unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.RequireUserId();
            var notifications = await _uow.Notifications.GetAllAsync(n => n.UserId == userId && n.Status == NotificationStatus.Unread);
            return Ok(new { count = notifications.Count() });
        }

        [HttpPut("me/{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(string notificationId)
        {
            var userId = User.RequireUserId();
            var notification = await _uow.Notifications.GetByIdAsync(notificationId);
            
            if (notification == null)
                return NotFound(new { message = "Notification not found" });
            
            if (notification.UserId != userId)
                return Forbid();
            
            notification.Status = NotificationStatus.Read;
            await _uow.Notifications.UpdateAsync(notification);
            await _uow.SaveChangesAsync();
            
            return Ok(new { message = "Notification marked as read" });
        }

        [HttpPut("me/read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.RequireUserId();
            var notifications = await _uow.Notifications.GetAllAsync(n => n.UserId == userId && n.Status == NotificationStatus.Unread);
            
            foreach (var notification in notifications)
            {
                notification.Status = NotificationStatus.Read;
                await _uow.Notifications.UpdateAsync(notification);
            }
            await _uow.SaveChangesAsync();
            
            return Ok(new { message = "All notifications marked as read", count = notifications.Count() });
        }
    }
}

