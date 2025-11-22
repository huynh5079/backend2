using BusinessLayer.DTOs.Payment;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers;

[ApiController]
[Route("tpedu/v1/payments/momo")]
public class PaymentsController : ControllerBase
{
    private readonly IMomoPaymentService _momoPaymentService;

    public PaymentsController(IMomoPaymentService momoPaymentService)
    {
        _momoPaymentService = momoPaymentService;
    }

    /// <summary>
    /// Tạo đơn thanh toán MoMo cho Escrow/WalletDeposit.
    /// </summary>
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] CreateMomoPaymentRequestDto request, CancellationToken ct)
    {
        var userId = User.RequireUserId();
        var response = await _momoPaymentService.CreatePaymentAsync(request, userId, ct);
        return Ok(response);
    }

    /// <summary>
    /// MoMo IPN callback. Không yêu cầu xác thực.
    /// </summary>
    [HttpPost("ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleIpn([FromBody] MomoIpnRequestDto request, CancellationToken ct)
    {
        var response = await _momoPaymentService.HandleIpnAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Query trạng thái thanh toán từ MoMo.
    /// </summary>
    [HttpGet("{paymentId}/query")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> QueryPayment(string paymentId, CancellationToken ct)
    {
        var response = await _momoPaymentService.QueryPaymentAsync(paymentId, ct);
        return Ok(response);
    }

    /// <summary>
    /// Refund giao dịch trên MoMo.
    /// </summary>
    [HttpPost("{paymentId}/refund")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RefundPayment(string paymentId, [FromBody] RefundPaymentRequestDto request, CancellationToken ct)
    {
        var response = await _momoPaymentService.RefundPaymentAsync(paymentId, request.Amount, request.Description ?? string.Empty, ct);
        return Ok(response);
    }
}

