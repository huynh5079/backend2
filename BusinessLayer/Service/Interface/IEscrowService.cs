using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;

namespace BusinessLayer.Service.Interface
{
    public interface IEscrowService
    {
        Task<OperationResult> PayEscrowAsync(string actorUserId, PayEscrowRequest req, CancellationToken ct = default);
        Task<OperationResult> ReleaseAsync(string adminUserId, ReleaseEscrowRequest req, CancellationToken ct = default);
        Task<OperationResult> RefundAsync(string adminUserId, RefundEscrowRequest req, CancellationToken ct = default);
    }
}


