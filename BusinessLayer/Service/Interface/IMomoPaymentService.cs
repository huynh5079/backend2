using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Payment;
using BusinessLayer.DTOs.Wallet;

namespace BusinessLayer.Service.Interface;

public interface IMomoPaymentService
{
    Task<CreateMomoPaymentResponseDto> CreatePaymentAsync(CreateMomoPaymentRequestDto request, string userId, CancellationToken ct = default);

    Task<MomoIpnResponseDto> HandleIpnAsync(MomoIpnRequestDto request, CancellationToken ct = default);

    Task<MomoQueryResponseDto> QueryPaymentAsync(string paymentId, CancellationToken ct = default);

    Task<MomoRefundResponseDto> RefundPaymentAsync(string paymentId, decimal amount, string description, CancellationToken ct = default);

    Task<OperationResult> RetryPaymentAsync(string paymentId, string userId, CancellationToken ct = default);
    
    Task<OperationResult> RetryPaymentByOrderIdAsync(string orderId, string userId, CancellationToken ct = default);
    
    Task<MomoIpnResponseDto> TestIpnByRequestIdAsync(string requestId, CancellationToken ct = default);
    
    Task<PaymentStatusDto> GetPaymentStatusAsync(string paymentId, string userId, CancellationToken ct = default);
}

