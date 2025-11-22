using DataLayer.Entities;
using DataLayer.Repositories.GenericType.Abstraction;
using System.Threading;
using System.Threading.Tasks;

namespace DataLayer.Repositories.Abstraction
{
    public interface INotificationRepository : IGenericRepository<Notification>
    {
        Task AddAsync(Notification entity, CancellationToken ct = default);
    }
}

