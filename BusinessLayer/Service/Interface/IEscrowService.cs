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
        Task<OperationResult> PartialReleaseAsync(string adminUserId, PartialReleaseEscrowRequest req, CancellationToken ct = default);
        Task<OperationResult> PartialRefundAsync(string adminUserId, PartialRefundEscrowRequest req, CancellationToken ct = default);
        /// <summary>
        /// Tính toán commission - GrossAmount sẽ tự động lấy từ Class.Price trong DB (không tin client)
        /// </summary>
        Task<CommissionCalculationDto> CalculateCommissionAsync(string classId, decimal? grossAmount = null, CancellationToken ct = default);
        
        // Tutor Deposit Flow
        Task<OperationResult> ProcessTutorDepositAsync(string tutorUserId, ProcessTutorDepositRequest req, CancellationToken ct = default);
        Task<OperationResult> ForfeitDepositAsync(string adminUserId, ForfeitDepositRequest req, CancellationToken ct = default);
    }
}


