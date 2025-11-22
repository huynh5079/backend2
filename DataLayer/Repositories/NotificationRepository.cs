using DataLayer.Entities;
using DataLayer.Repositories.Abstraction;
using DataLayer.Repositories.GenericType;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace DataLayer.Repositories
{
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        private readonly TpeduContext _context;

        public NotificationRepository(TpeduContext context) : base(context)
        {
            _context = context;
        }

        public Task AddAsync(Notification entity, CancellationToken ct = default)
            => _context.Notifications.AddAsync(entity, ct).AsTask();
    }
}

