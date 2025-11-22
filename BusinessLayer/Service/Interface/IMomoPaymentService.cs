using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Payment;

namespace BusinessLayer.Service.Interface;

public interface IMomoPaymentService
{
    Task<CreateMomoPaymentResponseDto> CreatePaymentAsync(CreateMomoPaymentRequestDto request, string userId, CancellationToken ct = default);

    Task<MomoIpnResponseDto> HandleIpnAsync(MomoIpnRequestDto request, CancellationToken ct = default);

    Task<MomoQueryResponseDto> QueryPaymentAsync(string paymentId, CancellationToken ct = default);

    Task<MomoRefundResponseDto> RefundPaymentAsync(string paymentId, decimal amount, string description, CancellationToken ct = default);
}

