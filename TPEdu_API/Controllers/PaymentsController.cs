using BusinessLayer.DTOs.Payment;
using BusinessLayer.DTOs.Wallet;
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
        // Log để theo dõi khi MoMo gọi IPN
        Console.WriteLine($"[IPN] ✅ Nhận IPN từ MoMo: OrderId={request.OrderId}, RequestId={request.RequestId}, ResultCode={request.ResultCode}, Amount={request.Amount}");
        
        var response = await _momoPaymentService.HandleIpnAsync(request, ct);
        
        // Log kết quả xử lý IPN
        if (response.ResultCode == 0)
        {
            Console.WriteLine($"[IPN] ✅ Xử lý IPN thành công: OrderId={request.OrderId}");
        }
        else
        {
            Console.WriteLine($"[IPN] ❌ Xử lý IPN thất bại: OrderId={request.OrderId}, Message={response.Message}");
        }
        
        return Ok(response);
    }

    /// <summary>
    /// Query trạng thái thanh toán từ MoMo (Admin only).
    /// </summary>
    [HttpGet("{paymentId}/query")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> QueryPayment(string paymentId, CancellationToken ct)
    {
        try
        {
            var response = await _momoPaymentService.QueryPaymentAsync(paymentId, ct);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Status = "Fail", Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Status = "Fail", Message = $"Lỗi hệ thống: {ex.Message}" });
        }
    }

    /// <summary>
    /// Retry payment processing cho payment của user hiện tại.
    /// Backend sẽ tự động:
    /// 1. Query payment status từ MoMo
    /// 2. Nếu MoMo báo thành công → Tự động cộng tiền vào ví
    /// 3. Tạo transaction
    /// 
    /// Đặc biệt hữu ích với MoMo demo vì không tự động gửi IPN.
    /// </summary>
    [HttpPost("{paymentId}/retry")]
    [Authorize]
    public async Task<IActionResult> RetryPayment(string paymentId, CancellationToken ct)
    {
        try
        {
            var userId = User.RequireUserId();
            Console.WriteLine($"[RetryPayment] 🔄 User {userId} đang retry payment {paymentId}");
            
            var response = await _momoPaymentService.RetryPaymentAsync(paymentId, userId, ct);
            
            if (response.Status == "Ok")
            {
                Console.WriteLine($"[RetryPayment] ✅ Thành công: Payment {paymentId} đã được xử lý và cộng tiền");
            }
            else
            {
                Console.WriteLine($"[RetryPayment] ❌ Thất bại: Payment {paymentId}, Message: {response.Message}");
            }
            
            if (response.Status == "Fail")
                return BadRequest(response);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"[RetryPayment] ❌ BadRequest: Payment {paymentId}, Error: {ex.Message}");
            return BadRequest(new { Status = "Fail", Message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RetryPayment] ❌ Error: Payment {paymentId}, Error: {ex.Message}");
            return StatusCode(500, new { Status = "Fail", Message = $"Lỗi hệ thống: {ex.Message}" });
        }
    }

    /// <summary>
    /// Retry payment processing bằng OrderId (tự động lấy từ response khi tạo payment).
    /// </summary>
    [HttpPost("retry-by-order/{orderId}")]
    [Authorize]
    public async Task<IActionResult> RetryPaymentByOrderId(string orderId, CancellationToken ct)
    {
        try
        {
            var userId = User.RequireUserId();
            var response = await _momoPaymentService.RetryPaymentByOrderIdAsync(orderId, userId, ct);
            if (response.Status == "Fail")
                return BadRequest(response);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Status = "Fail", Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Status = "Fail", Message = $"Lỗi hệ thống: {ex.Message}" });
        }
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

    /// <summary>
    /// Test IPN bằng RequestId: Tự động tìm payment bằng RequestId và test IPN (không cần Admin).
    /// </summary>
    [HttpPost("test-ipn/{requestId}")]
    [Authorize]
    public async Task<IActionResult> TestIpnByRequestId(string requestId, CancellationToken ct)
    {
        try
        {
            var response = await _momoPaymentService.TestIpnByRequestIdAsync(requestId, ct);
            if (response.ResultCode != 0)
                return BadRequest(response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Status = "Fail", Message = $"Lỗi hệ thống: {ex.Message}" });
        }
    }

    /// <summary>
    /// Lấy trạng thái payment của user (để biết thanh toán thành công chưa).
    /// </summary>
    [HttpGet("status/{paymentId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(string paymentId, CancellationToken ct)
    {
        try
        {
            var userId = User.RequireUserId();
            Console.WriteLine($"[GetPaymentStatus] Request: PaymentId={paymentId}, UserId={userId}");
            
            var response = await _momoPaymentService.GetPaymentStatusAsync(paymentId, userId, ct);
            
            Console.WriteLine($"[GetPaymentStatus] Success: PaymentId={paymentId}, Status={response.Status}, HasTransaction={response.HasTransaction}");
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"[GetPaymentStatus] Payment not found: PaymentId={paymentId}, Error={ex.Message}");
            return NotFound(new { Status = "Fail", Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[GetPaymentStatus] Unauthorized: PaymentId={paymentId}, Error={ex.Message}");
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetPaymentStatus] Error: PaymentId={paymentId}, Error={ex.Message}");
            return StatusCode(500, new { Status = "Fail", Message = $"Lỗi hệ thống: {ex.Message}" });
        }
    }
}

