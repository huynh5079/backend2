using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessLayer.DTOs.Payment;
using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Service;

public class MomoPaymentService : IMomoPaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MomoOptions _options;
    private readonly SystemWalletOptions _systemWalletOptions;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly IWalletService _walletService;
    private readonly ILogger<MomoPaymentService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private const string RequestTypeCaptureWallet = "captureWallet";

    public MomoPaymentService(
        IHttpClientFactory httpClientFactory,
        IOptions<MomoOptions> momoOptions,
        IOptions<SystemWalletOptions> systemWalletOptions,
        IUnitOfWork uow,
        INotificationService notificationService,
        IWalletService walletService,
        ILogger<MomoPaymentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = momoOptions.Value;
        _systemWalletOptions = systemWalletOptions.Value;
        _uow = uow;
        _notificationService = notificationService;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task<CreateMomoPaymentResponseDto> CreatePaymentAsync(CreateMomoPaymentRequestDto request, string userId, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Amount), "Amount must be greater than 0.");

        // Validate context
        switch (request.ContextType)
        {
            case PaymentContextType.Escrow:
                var escrow = await _uow.Escrows.GetByIdAsync(request.ContextId, ct);
                if (escrow == null)
                    throw new ArgumentException("Escrow not found.", nameof(request.ContextId));
                break;

            case PaymentContextType.WalletDeposit:
                var user = await _uow.Users.GetByIdAsync(request.ContextId);
                if (user == null)
                    throw new ArgumentException("User not found.", nameof(request.ContextId));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request.ContextType));
        }

        var payment = new Payment
        {
            Provider = PaymentProvider.MoMo,
            OrderId = GenerateOrderId(),
            RequestId = Guid.NewGuid().ToString(),
            Amount = request.Amount,
            Currency = "VND",
            Status = PaymentStatus.Pending,
            ContextType = request.ContextType,
            ContextId = request.ContextType == PaymentContextType.WalletDeposit ? request.ContextId : request.ContextId,
            Message = request.Description,
            ExtraData = request.ExtraData,
        };

        await _uow.Payments.AddAsync(payment, ct);

        var momoRequest = BuildCreateRequest(payment, request.Description);
        await _uow.PaymentLogs.AddAsync(new PaymentLog
        {
            PaymentId = payment.Id,
            Event = "Create.Request",
            Payload = JsonSerializer.Serialize(momoRequest, _jsonOptions)
        }, ct);

        var httpClient = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.EndpointCreate)
        {
            Content = new StringContent(JsonSerializer.Serialize(momoRequest, _jsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        await _uow.PaymentLogs.AddAsync(new PaymentLog
        {
            PaymentId = payment.Id,
            Event = "Create.Response",
            Payload = responseContent
        }, ct);

        var momoResponse = JsonSerializer.Deserialize<MomoCreateResponse>(responseContent, _jsonOptions)
            ?? throw new InvalidOperationException("MoMo create payment response is invalid.");

        payment.ResultCode = momoResponse.ResultCode;
        payment.Message = momoResponse.Message;

        if (momoResponse.ResultCode != 0)
        {
            payment.Status = PaymentStatus.Failed;
            await _uow.SaveChangesAsync();
            throw new InvalidOperationException($"MoMo create payment failed: {momoResponse.Message} (code {momoResponse.ResultCode})");
        }

        await _uow.SaveChangesAsync();

        return new CreateMomoPaymentResponseDto
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            PayUrl = momoResponse.PayUrl ?? momoResponse.ShortLink ?? string.Empty,
            Deeplink = momoResponse.Deeplink,
            Provider = PaymentProvider.MoMo.ToString()
        };
    }

    public async Task<MomoIpnResponseDto> HandleIpnAsync(MomoIpnRequestDto request, CancellationToken ct = default)
    {
        try
        {
            if (!ValidateIpnSignature(request))
            {
                _logger.LogWarning("MoMo IPN signature invalid for order {OrderId}", request.OrderId);
                return new MomoIpnResponseDto { ResultCode = 1, Message = "INVALID_SIGNATURE" };
            }

            var payment = await _uow.Payments.GetByOrderIdAsync(PaymentProvider.MoMo, request.OrderId, ct);
            if (payment == null)
            {
                _logger.LogWarning("MoMo IPN received for unknown order {OrderId}", request.OrderId);
                return new MomoIpnResponseDto { ResultCode = 0, Message = "ORDER_NOT_FOUND" };
            }

            await _uow.PaymentLogs.AddAsync(new PaymentLog
            {
                PaymentId = payment.Id,
                Event = "IPN",
                Payload = JsonSerializer.Serialize(request, _jsonOptions)
            }, ct);

            payment.ResultCode = request.ResultCode;
            payment.Message = request.Message;

            if (request.ResultCode == 0)
            {
                if (payment.Status != PaymentStatus.Paid)
                {
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = DateTime.UtcNow;
                    payment.TransactionId = request.TransId;

                    await ApplyPaymentSuccessAsync(payment, request, ct);

                    await _uow.SaveChangesAsync();
                }
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                await _uow.SaveChangesAsync();
            }

            return new MomoIpnResponseDto { ResultCode = 0, Message = "OK" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return new MomoIpnResponseDto { ResultCode = 1, Message = "INTERNAL_ERROR" };
        }
    }

    public async Task<MomoQueryResponseDto> QueryPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        var payment = await _uow.Payments.GetByIdAsync(paymentId);
        if (payment == null)
            throw new ArgumentException("Payment not found.", nameof(paymentId));

        var requestId = Guid.NewGuid().ToString();

        var rawData = new List<KeyValuePair<string, string>>
        {
            new("accessKey", _options.AccessKey),
            new("orderId", payment.OrderId),
            new("partnerCode", _options.PartnerCode),
            new("requestId", requestId)
        };

        var signature = Sign(rawData);

        var queryRequest = new
        {
            partnerCode = _options.PartnerCode,
            requestId,
            orderId = payment.OrderId,
            lang = "vi",
            signature
        };

        var httpClient = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.EndpointQuery)
        {
            Content = new StringContent(JsonSerializer.Serialize(queryRequest, _jsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        await _uow.PaymentLogs.AddAsync(new PaymentLog
        {
            PaymentId = payment.Id,
            Event = "Query.Response",
            Payload = responseContent
        }, ct);

        var momoResponse = JsonSerializer.Deserialize<MomoQueryResponse>(responseContent, _jsonOptions)
            ?? throw new InvalidOperationException("MoMo query response invalid.");

        return new MomoQueryResponseDto
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            ResultCode = momoResponse.ResultCode,
            Message = momoResponse.Message,
            TransId = momoResponse.TransId,
            Status = momoResponse.ResultCode == 0 ? "SUCCESS" : "FAILED",
            Amount = momoResponse.Amount,
            ResponseTime = momoResponse.ResponseTime
        };
    }

    public async Task<MomoRefundResponseDto> RefundPaymentAsync(string paymentId, decimal amount, string description, CancellationToken ct = default)
    {
        var payment = await _uow.Payments.GetByIdAsync(paymentId);
        if (payment == null)
            throw new ArgumentException("Payment not found.", nameof(paymentId));

        if (payment.Status != PaymentStatus.Paid)
            throw new InvalidOperationException("Only successful payments can be refunded.");

        if (string.IsNullOrWhiteSpace(payment.TransactionId))
            throw new InvalidOperationException("Payment missing MoMo transaction id.");

        var requestId = Guid.NewGuid().ToString();
        var refundId = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 32);

        var rawData = new List<KeyValuePair<string, string>>
        {
            new("accessKey", _options.AccessKey),
            new("amount", ((long)amount).ToString()),
            new("description", description ?? string.Empty),
            new("orderId", payment.OrderId),
            new("partnerCode", _options.PartnerCode),
            new("requestId", requestId),
            new("transId", payment.TransactionId!)
        };

        var signature = Sign(rawData);

        var refundRequest = new
        {
            partnerCode = _options.PartnerCode,
            requestId,
            orderId = payment.OrderId,
            amount = ((long)amount).ToString(),
            transId = payment.TransactionId,
            lang = "vi",
            description = description ?? string.Empty,
            signature
        };

        var httpClient = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.EndpointRefund)
        {
            Content = new StringContent(JsonSerializer.Serialize(refundRequest, _jsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        await _uow.PaymentLogs.AddAsync(new PaymentLog
        {
            PaymentId = payment.Id,
            Event = "Refund.Response",
            Payload = responseContent
        }, ct);

        var momoResponse = JsonSerializer.Deserialize<MomoRefundResponse>(responseContent, _jsonOptions)
            ?? throw new InvalidOperationException("MoMo refund response invalid.");

        if (momoResponse.ResultCode == 0)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.ResultCode = momoResponse.ResultCode;
            payment.Message = momoResponse.Message;
            await _uow.SaveChangesAsync();
        }

        return new MomoRefundResponseDto
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            RefundId = refundId,
            ResultCode = momoResponse.ResultCode,
            Message = momoResponse.Message,
            Amount = momoResponse.Amount,
            ResponseTime = momoResponse.ResponseTime
        };
    }

    #region Helpers

    private string GenerateOrderId()
    {
        var random = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"MM-{DateTime.UtcNow:yyyyMMdd}-{random}";
    }

    private MomoCreateRequest BuildCreateRequest(Payment payment, string? description)
    {
        var amount = ((long)payment.Amount).ToString();
        var extraData = payment.ExtraData ?? string.Empty;
        var orderInfo = description ?? $"Payment for {payment.ContextType}";

        var rawData = new List<KeyValuePair<string, string>>
        {
            new("accessKey", _options.AccessKey),
            new("amount", amount),
            new("extraData", extraData),
            new("ipnUrl", _options.NotifyUrl),
            new("orderId", payment.OrderId),
            new("orderInfo", orderInfo),
            new("partnerCode", _options.PartnerCode),
            new("redirectUrl", _options.ReturnUrl),
            new("requestId", payment.RequestId),
            new("requestType", RequestTypeCaptureWallet)
        };

        var signature = Sign(rawData);

        return new MomoCreateRequest
        {
            PartnerCode = _options.PartnerCode,
            PartnerName = "TPEdu",
            StoreId = "TPEdu",
            OrderId = payment.OrderId,
            Amount = amount,
            Lang = "vi",
            OrderInfo = orderInfo,
            RequestId = payment.RequestId,
            RedirectUrl = _options.ReturnUrl,
            IpnUrl = _options.NotifyUrl,
            ExtraData = extraData,
            RequestType = RequestTypeCaptureWallet,
            Signature = signature
        };
    }

    private bool ValidateIpnSignature(MomoIpnRequestDto request)
    {
        var rawData = new List<KeyValuePair<string, string>>
        {
            new("accessKey", request.AccessKey),
            new("amount", request.Amount.ToString()),
            new("extraData", request.ExtraData ?? string.Empty),
            new("message", request.Message),
            new("orderId", request.OrderId),
            new("orderInfo", request.OrderInfo),
            new("orderType", request.OrderType),
            new("partnerCode", request.PartnerCode),
            new("payType", request.PayType),
            new("requestId", request.RequestId),
            new("responseTime", request.ResponseTime.ToString()),
            new("resultCode", request.ResultCode.ToString()),
            new("transId", request.TransId)
        };

        var signature = Sign(rawData);
        return string.Equals(signature, request.Signature, StringComparison.OrdinalIgnoreCase);
    }

    private string Sign(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var raw = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task ApplyPaymentSuccessAsync(Payment payment, MomoIpnRequestDto request, CancellationToken ct)
    {
        switch (payment.ContextType)
        {
            case PaymentContextType.Escrow:
                await ApplyEscrowPaymentAsync(payment, request, ct);
                break;

            case PaymentContextType.WalletDeposit:
                await ApplyWalletDepositAsync(payment, request, ct);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(payment.ContextType));
        }
    }

    private async Task ApplyEscrowPaymentAsync(Payment payment, MomoIpnRequestDto request, CancellationToken ct)
    {
        var escrow = await _uow.Escrows.GetByIdAsync(payment.ContextId, ct);
        if (escrow == null)
        {
            _logger.LogWarning("Escrow {EscrowId} not found when processing MoMo payment {PaymentId}", payment.ContextId, payment.Id);
            return;
        }

        if (escrow.Status == EscrowStatus.Held)
        {
            _logger.LogInformation("Escrow {EscrowId} already marked as held", escrow.Id);
            return;
        }

        var adminWallet = await _uow.Wallets.GetByUserIdAsync(_systemWalletOptions.SystemWalletUserId, ct)
            ?? await CreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);

        adminWallet.Balance += payment.Amount;
        await _uow.Wallets.Update(adminWallet);

        await _uow.Transactions.AddAsync(new Transaction
        {
            WalletId = adminWallet.Id,
            Type = TransactionType.EscrowIn,
            Status = TransactionStatus.Succeeded,
            Amount = payment.Amount,
            Note = $"MoMo escrow payment {payment.OrderId}",
            CounterpartyUserId = escrow.PayerUserId
        }, ct);

        escrow.Status = EscrowStatus.Held;
        escrow.PayerUserId = escrow.PayerUserId ?? payment.ContextId;

        var notification = await _notificationService.CreateEscrowNotificationAsync(
            escrow.PayerUserId ?? string.Empty,
            NotificationType.EscrowPaid,
            payment.Amount,
            escrow.ClassId ?? string.Empty,
            payment.Id,
            ct);

        await _notificationService.SendRealTimeNotificationAsync(escrow.PayerUserId ?? string.Empty, notification, ct);
    }

    private async Task ApplyWalletDepositAsync(Payment payment, MomoIpnRequestDto request, CancellationToken ct)
    {
        var wallet = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
        wallet.Balance += payment.Amount;
        await _uow.Wallets.Update(wallet);

        var transaction = new Transaction
        {
            WalletId = wallet.Id,
            Type = TransactionType.Credit,
            Status = TransactionStatus.Succeeded,
            Amount = payment.Amount,
            Note = $"MoMo wallet deposit {payment.OrderId}",
            CounterpartyUserId = payment.ContextId
        };

        await _uow.Transactions.AddAsync(transaction, ct);

        var notification = await _notificationService.CreateWalletNotificationAsync(
            payment.ContextId,
            NotificationType.WalletDeposit,
            payment.Amount,
            $"Nạp ví qua MoMo (order {payment.OrderId})",
            payment.Id,
            ct);

        await _notificationService.SendRealTimeNotificationAsync(payment.ContextId, notification, ct);
    }

    private async Task<Wallet> CreateWalletAsync(string userId, CancellationToken ct)
    {
        var wallet = new Wallet { UserId = userId, Balance = 0m, Currency = "VND", IsFrozen = false };
        await _uow.Wallets.AddAsync(wallet, ct);
        await _uow.SaveChangesAsync();
        return wallet;
    }

    #endregion

    #region Internal DTOs

    private sealed class MomoCreateRequest
    {
        public string PartnerCode { get; set; } = default!;
        public string PartnerName { get; set; } = default!;
        public string StoreId { get; set; } = default!;
        public string RequestId { get; set; } = default!;
        public string OrderId { get; set; } = default!;
        public string Amount { get; set; } = default!;
        public string Lang { get; set; } = "vi";
        public string OrderInfo { get; set; } = default!;
        public string RedirectUrl { get; set; } = default!;
        public string IpnUrl { get; set; } = default!;
        public string ExtraData { get; set; } = string.Empty;
        public string RequestType { get; set; } = default!;
        public string Signature { get; set; } = default!;
    }

    private sealed class MomoCreateResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PayUrl { get; set; }
        public string? Deeplink { get; set; }
        public string? ShortLink { get; set; }
    }

    private sealed class MomoQueryResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TransId { get; set; }
        public long Amount { get; set; }
        public long ResponseTime { get; set; }
    }

    private sealed class MomoRefundResponse
    {
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public long Amount { get; set; }
        public long ResponseTime { get; set; }
    }

    #endregion
}

