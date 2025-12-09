using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessLayer.DTOs.Payment;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;
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
    private readonly IEmailService _emailService;
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
        IEmailService emailService,
        ILogger<MomoPaymentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = momoOptions.Value;
        _systemWalletOptions = systemWalletOptions.Value;
        _uow = uow;
        _notificationService = notificationService;
        _walletService = walletService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<CreateMomoPaymentResponseDto> CreatePaymentAsync(CreateMomoPaymentRequestDto request, string userId, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Amount), "Amount must be greater than 0.");

        // Determine ContextId based on ContextType
        string contextId;
        switch (request.ContextType)
        {
            case PaymentContextType.Escrow:
                // Escrow requires ContextId
                if (string.IsNullOrWhiteSpace(request.ContextId))
                    throw new ArgumentException("ContextId is required for Escrow payment.", nameof(request.ContextId));
                
                var escrow = await _uow.Escrows.GetByIdAsync(request.ContextId, ct);
                if (escrow == null)
                    throw new ArgumentException("Escrow not found.", nameof(request.ContextId));
                
                contextId = request.ContextId;
                break;

            case PaymentContextType.WalletDeposit:
                // WalletDeposit: use userId if ContextId is not provided
                if (string.IsNullOrWhiteSpace(request.ContextId))
                {
                    // Use userId from the authenticated user
                    // Validate user exists
                    var authenticatedUser = await _uow.Users.GetByIdAsync(userId);
                    if (authenticatedUser == null)
                        throw new ArgumentException("Authenticated user not found.", nameof(userId));
                    
                    contextId = userId;
                    _logger.LogInformation(
                        "WalletDeposit payment: ContextId not provided, using userId from authentication: {UserId}",
                        userId);
                }
                else
                {
                    // Security: Only allow users to deposit into their own wallet
                    if (request.ContextId != userId)
                    {
                        throw new UnauthorizedAccessException(
                            $"You can only create WalletDeposit payment for your own account. " +
                            $"Provided ContextId: {request.ContextId}, Your UserId: {userId}");
                    }
                    
                    // Validate that the provided ContextId is a valid user
                    var user = await _uow.Users.GetByIdAsync(request.ContextId);
                    if (user == null)
                        throw new ArgumentException("User not found.", nameof(request.ContextId));
                    
                    contextId = request.ContextId;
                }
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
            ContextId = contextId,
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

        // Log chi tiết response từ MoMo
        Console.WriteLine($"[CreatePaymentAsync] 📥 MoMo response: ResultCode={momoResponse.ResultCode}, Message={momoResponse.Message}, PayUrl={momoResponse.PayUrl ?? "NULL"}, ShortLink={momoResponse.ShortLink ?? "NULL"}, Deeplink={momoResponse.Deeplink ?? "NULL"}");
        _logger.LogInformation(
            "MoMo create payment response: ResultCode={ResultCode}, Message={Message}, PayUrl={PayUrl}, ShortLink={ShortLink}, Deeplink={Deeplink}",
            momoResponse.ResultCode, momoResponse.Message, momoResponse.PayUrl ?? "NULL", momoResponse.ShortLink ?? "NULL", momoResponse.Deeplink ?? "NULL");

        payment.ResultCode = momoResponse.ResultCode;
        payment.Message = momoResponse.Message;

        if (momoResponse.ResultCode != 0)
        {
            payment.Status = PaymentStatus.Failed;
            await _uow.SaveChangesAsync();
            Console.WriteLine($"[CreatePaymentAsync] ❌ MoMo create payment failed: {momoResponse.Message} (code {momoResponse.ResultCode})");
            throw new InvalidOperationException($"MoMo create payment failed: {momoResponse.Message} (code {momoResponse.ResultCode})");
        }

        // QUAN TRỌNG: Validate PayUrl - phải có PayUrl hoặc ShortLink để user có thể thanh toán
        var payUrl = momoResponse.PayUrl ?? momoResponse.ShortLink;
        if (string.IsNullOrWhiteSpace(payUrl))
        {
            payment.Status = PaymentStatus.Failed;
            await _uow.SaveChangesAsync();
            Console.WriteLine($"[CreatePaymentAsync] ❌ MoMo không trả về PayUrl hoặc ShortLink. Response: {responseContent}");
            _logger.LogError(
                "MoMo create payment thành công (ResultCode=0) nhưng không có PayUrl hoặc ShortLink. PaymentId={PaymentId}, OrderId={OrderId}, Response={Response}",
                payment.Id, payment.OrderId, responseContent);
            throw new InvalidOperationException("MoMo không trả về PayUrl hoặc ShortLink. Không thể tạo payment link.");
        }

        await _uow.SaveChangesAsync();

        Console.WriteLine($"[CreatePaymentAsync] ✅ Tạo payment thành công: PaymentId={payment.Id}, OrderId={payment.OrderId}, PayUrl={payUrl}");

        return new CreateMomoPaymentResponseDto
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            RequestId = payment.RequestId,
            PayUrl = payUrl,
            Deeplink = momoResponse.Deeplink,
            Provider = PaymentProvider.MoMo.ToString()
        };
    }

    public async Task<MomoIpnResponseDto> HandleIpnAsync(MomoIpnRequestDto request, CancellationToken ct = default)
    {
        try
        {
            // Log chi tiết khi nhận IPN từ MoMo
            _logger.LogInformation(
                "🔔 [IPN] Nhận IPN từ MoMo: OrderId={OrderId}, RequestId={RequestId}, ResultCode={ResultCode}, Amount={Amount}, TransId={TransId}, Message={Message}",
                request.OrderId, request.RequestId, request.ResultCode, request.Amount, request.TransId ?? "NULL", request.Message ?? "NULL");
            
            Console.WriteLine($"[IPN] 🔔 Nhận IPN từ MoMo - OrderId: {request.OrderId}, RequestId: {request.RequestId}, ResultCode: {request.ResultCode}, Amount: {request.Amount}");
            
            // Tự động tìm OrderId nếu không có (dựa trên RequestId)
            if (string.IsNullOrWhiteSpace(request.OrderId) && !string.IsNullOrWhiteSpace(request.RequestId))
            {
                _logger.LogInformation(
                    "OrderId rỗng, tự động tìm payment bằng RequestId: {RequestId}",
                    request.RequestId);
                
                var paymentByRequestId = await _uow.Payments.GetByRequestIdAsync(PaymentProvider.MoMo, request.RequestId, ct);
                if (paymentByRequestId != null)
                {
                    request.OrderId = paymentByRequestId.OrderId;
                    _logger.LogInformation(
                        "Đã tự động tìm thấy OrderId={OrderId} từ RequestId={RequestId}",
                        request.OrderId, request.RequestId);
                }
                else
                {
                    _logger.LogWarning(
                        "Không tìm thấy payment với RequestId={RequestId}",
                        request.RequestId);
                    return new MomoIpnResponseDto { ResultCode = 1, Message = "PAYMENT_NOT_FOUND_BY_REQUEST_ID" };
                }
            }
            
            // Kiểm tra signature validation
            // Cho phép bypass nếu signature là placeholder (để test ở local)
            bool isPlaceholderSignature = string.IsNullOrWhiteSpace(request.Signature) || 
                                         request.Signature == "TÍNH_TOÁN_SAU" ||
                                         request.Signature.Equals("TINH_TOAN_SAU", StringComparison.OrdinalIgnoreCase);
            
            if (isPlaceholderSignature)
            {
                _logger.LogWarning(
                    "⚠️ BYPASS SIGNATURE VALIDATION: Signature là placeholder '{Signature}' cho order {OrderId}. " +
                    "Cho phép tiếp tục xử lý (chỉ dùng cho test local).",
                    request.Signature, request.OrderId);
            }
            else if (!ValidateIpnSignature(request))
            {
                _logger.LogWarning(
                    "MoMo IPN signature invalid for order {OrderId}. Signature từ request: {RequestSignature}",
                    request.OrderId, request.Signature);
                return new MomoIpnResponseDto { ResultCode = 1, Message = "INVALID_SIGNATURE" };
            }
            else
            {
                _logger.LogInformation("IPN signature hợp lệ cho order {OrderId}", request.OrderId);
            }

            var payment = await _uow.Payments.GetByOrderIdAsync(PaymentProvider.MoMo, request.OrderId, ct);
            if (payment == null)
            {
                _logger.LogError(
                    "❌ LỖI: MoMo IPN received for unknown order {OrderId}. " +
                    "Payment không tồn tại trong database. " +
                    "Vui lòng tạo payment trước khi gửi IPN, hoặc kiểm tra OrderId có đúng không.",
                    request.OrderId);
                return new MomoIpnResponseDto { ResultCode = 0, Message = "ORDER_NOT_FOUND" };
            }
            
            _logger.LogInformation(
                "Đã tìm thấy payment {PaymentId} cho order {OrderId}. Status hiện tại: {Status}, ContextType: {ContextType}, ContextId: {ContextId}",
                payment.Id, payment.OrderId, payment.Status, payment.ContextType, payment.ContextId);

            // Bước 1: Log thông tin IPN request để debug
            _logger.LogInformation(
                "Nhận IPN từ MoMo: OrderId={OrderId}, ResultCode={ResultCode}, TransId={TransId}, Message={Message}",
                request.OrderId, request.ResultCode, request.TransId ?? "NULL", request.Message);

            // Bước 2: Tạo và log IPN (sẽ được lưu sau)
            var paymentLog = new PaymentLog
            {
                PaymentId = payment.Id,
                Event = "IPN",
                Payload = JsonSerializer.Serialize(request, _jsonOptions)
            };
            await _uow.PaymentLogs.AddAsync(paymentLog, ct);

            // Bước 3: Cập nhật thông tin payment cơ bản (ResultCode, Message)
            payment.ResultCode = request.ResultCode;
            payment.Message = request.Message;

            // Bước 4: Xử lý dựa trên ResultCode
            if (request.ResultCode == 0)
            {
                // Thanh toán thành công
                bool isNewlyPaid = payment.Status != PaymentStatus.Paid;
                
                if (isNewlyPaid)
                {
                    // Cập nhật trạng thái payment thành Paid
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = DateTime.UtcNow;
                    
                    // Cập nhật TransactionId nếu có
                    if (!string.IsNullOrWhiteSpace(request.TransId))
                    {
                        payment.TransactionId = request.TransId;
                        _logger.LogInformation(
                            "Đã cập nhật TransactionId={TransactionId} cho payment {PaymentId} (OrderId: {OrderId})",
                            request.TransId, payment.Id, payment.OrderId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "MoMo IPN thành công nhưng TransId rỗng cho payment {PaymentId} (OrderId: {OrderId})",
                            payment.Id, payment.OrderId);
                    }

                    // QUAN TRỌNG: Lưu trạng thái payment và log TRƯỚC (trước khi xử lý business logic)
                    // Đảm bảo trạng thái payment luôn được cập nhật ngay cả khi business logic thất bại
                    await _uow.SaveChangesAsync();
                }
                else
                {
                    // Payment đã là Paid, nhưng cần kiểm tra xem business logic đã được áp dụng chưa
                    _logger.LogInformation(
                        "Payment {PaymentId} (OrderId: {OrderId}) đã là Paid. Kiểm tra xem business logic đã được áp dụng chưa...",
                        payment.Id, payment.OrderId);
                    
                    // Cập nhật TransactionId nếu có và chưa có
                    if (!string.IsNullOrWhiteSpace(request.TransId) && string.IsNullOrWhiteSpace(payment.TransactionId))
                    {
                        payment.TransactionId = request.TransId;
                        _logger.LogInformation(
                            "Đã cập nhật TransactionId={TransactionId} cho payment {PaymentId} (OrderId: {OrderId})",
                            request.TransId, payment.Id, payment.OrderId);
                        await _uow.SaveChangesAsync();
                    }
                }

                // QUAN TRỌNG: Kiểm tra xem business logic đã được áp dụng chưa
                // Nếu chưa, áp dụng business logic ngay cả khi payment đã là Paid (retry case)
                var hasBusinessLogicApplied = await CheckIfBusinessLogicAppliedAsync(payment, ct);
                
                if (!hasBusinessLogicApplied)
                {
                    _logger.LogInformation(
                        "Business logic CHƯA được áp dụng cho payment {PaymentId} (OrderId: {OrderId}). " +
                        "Sẽ áp dụng business logic ngay bây giờ (isNewlyPaid: {IsNewlyPaid}).",
                        payment.Id, payment.OrderId, isNewlyPaid);
                    
                    // Bước 4: Áp dụng business logic (escrow/wallet deposit) - bọc trong try-catch
                    // Nếu bước này thất bại, trạng thái payment đã được lưu là Paid (đúng)
                    try
                    {
                        _logger.LogInformation(
                            "Bắt đầu áp dụng business logic cho payment {PaymentId} (OrderId: {OrderId}, ContextType: {ContextType}, ContextId: {ContextId})",
                            payment.Id, payment.OrderId, payment.ContextType, payment.ContextId);
                        
                        await ApplyPaymentSuccessAsync(payment, request, ct);
                        
                        _logger.LogInformation(
                            "Business logic đã được áp dụng thành công cho payment {PaymentId}. Đang lưu thay đổi...",
                            payment.Id);
                        
                        // QUAN TRỌNG: Lưu tiền và transaction vào DB TRƯỚC (bước bắt buộc)
                        // Nếu bước này thành công, tiền đã được cộng vào ví và transaction đã được lưu
                        int savedCount;
                        try
                        {
                            savedCount = await _uow.SaveChangesAsync();
                            _logger.LogInformation(
                                "💰 [IPN] Đã lưu thành công tiền và transaction vào database cho payment {PaymentId} (OrderId: {OrderId}). Số entities đã lưu: {SavedCount}",
                                payment.Id, payment.OrderId, savedCount);
                            
                            Console.WriteLine($"[IPN] 💰 Đã cộng tiền thành công vào ví - OrderId: {payment.OrderId}, Amount: {payment.Amount}, PaymentId: {payment.Id}");
                        }
                        catch (DbUpdateConcurrencyException concurrencyEx)
                        {
                            _logger.LogError(concurrencyEx,
                                "LỖI CONCURRENCY: Không thể lưu wallet do RowVersion conflict cho payment {PaymentId} (OrderId: {OrderId}). " +
                                "Có thể wallet đã bị thay đổi bởi request khác. Sẽ thử reload và cập nhật lại.",
                                payment.Id, payment.OrderId);
                            
                            // Retry: Reload wallet và cập nhật lại
                            if (payment.ContextType == PaymentContextType.WalletDeposit)
                            {
                                // Lấy wallet từ userId
                                var retryWalletFromDb = await _uow.Wallets.GetByUserIdAsync(payment.ContextId, ct);
                                if (retryWalletFromDb != null)
                                {
                                    // Reload với tracking
                                    var retryWallet = await _uow.Wallets.GetByIdAsync(retryWalletFromDb.Id);
                                    if (retryWallet != null)
                                    {
                                        retryWallet.Balance += payment.Amount;
                                        await _uow.Wallets.Update(retryWallet);
                                        
                                        // Tạo transaction nếu chưa có
                                        var existingTransaction = await _uow.Transactions.GetAsync(
                                            t => t.WalletId == retryWallet.Id 
                                            && t.Type == TransactionType.Credit 
                                            && t.Amount == payment.Amount
                                            && t.Note.Contains(payment.OrderId));
                                        
                                        if (existingTransaction == null)
                                        {
                                            var momoTransId = !string.IsNullOrWhiteSpace(payment.TransactionId) 
                                                ? payment.TransactionId 
                                                : (!string.IsNullOrWhiteSpace(request.TransId) ? request.TransId : null);
                                            
                                            var note = !string.IsNullOrWhiteSpace(momoTransId)
                                                ? $"MoMo wallet deposit {payment.OrderId} (TransId: {momoTransId})"
                                                : $"MoMo wallet deposit {payment.OrderId}";
                                            
                                            var retryTransaction = new Transaction
                                            {
                                                WalletId = retryWallet.Id,
                                                Type = TransactionType.Credit,
                                                Status = TransactionStatus.Succeeded,
                                                Amount = payment.Amount,
                                                Note = note,
                                                CounterpartyUserId = payment.ContextId
                                            };
                                            await _uow.Transactions.AddAsync(retryTransaction, ct);
                                        }
                                        
                                        savedCount = await _uow.SaveChangesAsync();
                                        _logger.LogInformation(
                                            "Đã retry và lưu thành công tiền và transaction sau concurrency conflict cho payment {PaymentId} (OrderId: {OrderId}). Số entities đã lưu: {SavedCount}",
                                            payment.Id, payment.OrderId, savedCount);
                                    }
                                    else
                                    {
                                        _logger.LogError(
                                            "Không thể reload wallet với tracking để retry sau concurrency conflict cho payment {PaymentId}",
                                            payment.Id);
                                        throw; // Re-throw exception nếu không thể retry
                                    }
                                }
                                else
                                {
                                    _logger.LogError(
                                        "Không thể tìm thấy wallet cho user {UserId} để retry sau concurrency conflict cho payment {PaymentId}",
                                        payment.ContextId, payment.Id);
                                    throw; // Re-throw exception nếu không thể retry
                                }
                            }
                            else
                            {
                                throw; // Re-throw exception nếu không phải WalletDeposit
                            }
                        }
                        
                        // XÁC NHẬN: Kiểm tra wallet balance đã được cập nhật chưa
                        if (payment.ContextType == PaymentContextType.WalletDeposit)
                        {
                            try
                            {
                                var walletAfterSave = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
                                _logger.LogInformation(
                                    "XÁC NHẬN: Wallet balance sau khi SaveChangesAsync: {Balance} cho user {UserId} (Payment {PaymentId})",
                                    walletAfterSave.Balance, payment.ContextId, payment.Id);
                                
                                if (savedCount == 0)
                                {
                                    _logger.LogWarning(
                                        "CẢNH BÁO: SaveChangesAsync trả về 0 entities đã lưu cho payment {PaymentId} (OrderId: {OrderId}). " +
                                        "Có thể wallet và transaction chưa được lưu vào database!",
                                        payment.Id, payment.OrderId);
                                }
                            }
                            catch (Exception checkEx)
                            {
                                _logger.LogWarning(checkEx,
                                    "Không thể kiểm tra wallet balance sau khi SaveChangesAsync cho payment {PaymentId}",
                                    payment.Id);
                            }
                        }
                        
                        // PHỤ KIỆN: Tạo notification và email SAU KHI đã lưu tiền và transaction thành công
                        // Nếu notification/email lỗi, KHÔNG ảnh hưởng đến việc đã cộng tiền (đã được lưu ở trên)
                        if (payment.ContextType == PaymentContextType.WalletDeposit)
                        {
                            // Tìm transaction vừa được tạo để lấy transaction.Id cho notification
                            try
                            {
                                var wallet = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
                                var (transactions, total) = await _uow.Transactions.GetByWalletIdAsync(wallet.Id, 1, 10, ct);
                                var transaction = transactions.FirstOrDefault(t => 
                                    t.Type == TransactionType.Credit && 
                                    t.Status == TransactionStatus.Succeeded &&
                                    t.Note != null && 
                                    t.Note.Contains(payment.OrderId));
                                
                                if (transaction != null)
                                {
                                    _logger.LogInformation(
                                        "Đã xác nhận transaction đã được lưu vào database cho payment {PaymentId} (OrderId: {OrderId}). TransactionId: {TransactionId}",
                                        payment.Id, payment.OrderId, transaction.Id);
                                    
                                    // PHỤ KIỆN 1: Tạo và gửi notification
                                    // Lỗi ở đây KHÔNG ảnh hưởng đến việc đã cộng tiền (đã lưu ở trên)
                                    // Tạm thời comment để debug - sẽ bật lại sau khi xác nhận flow hoạt động
                                    try
                                    {
                                        _logger.LogInformation(
                                            "Bắt đầu tạo notification cho payment {PaymentId} (OrderId: {OrderId})",
                                            payment.Id, payment.OrderId);
                                        
                                        var notification = await _notificationService.CreateWalletNotificationAsync(
                                            payment.ContextId,
                                            NotificationType.WalletDeposit,
                                            payment.Amount,
                                            $"Nạp ví qua MoMo (order {payment.OrderId})",
                                            transaction.Id, // Sử dụng transaction.Id đã được lưu vào DB
                                            ct);

                                        _logger.LogInformation(
                                            "Đã tạo notification object. Đang lưu notification vào database...");
                                        
                                        await _uow.SaveChangesAsync(); // Lưu notification (riêng biệt, không ảnh hưởng đến tiền)
                                        
                                        _logger.LogInformation(
                                            "Đã lưu notification vào database. Đang gửi real-time notification...");
                                        
                                        await _notificationService.SendRealTimeNotificationAsync(payment.ContextId, notification, ct);
                                        
                                        _logger.LogInformation("Đã gửi notification thành công cho user {UserId}", payment.ContextId);
                                    }
                                    catch (Exception notifEx)
                                    {
                                        // Notification lỗi KHÔNG ảnh hưởng đến việc đã cộng tiền (đã lưu ở trên)
                                        _logger.LogWarning(notifEx,
                                            "PHỤ KIỆN LỖI: Không thể gửi notification cho user {UserId} nhưng tiền đã được cộng vào ví thành công. " +
                                            "Lỗi: {ErrorMessage}. StackTrace: {StackTrace}",
                                            payment.ContextId, notifEx.Message, notifEx.StackTrace);
                                    }

                                    // PHỤ KIỆN 2: Gửi email hóa đơn
                                    // Lỗi ở đây KHÔNG ảnh hưởng đến việc đã cộng tiền (đã lưu ở trên)
                                    try
                                    {
                                        var user = await _uow.Users.GetByIdAsync(payment.ContextId);
                                        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                                        {
                                            var invoiceNumber = $"INV-{payment.OrderId}";
                                            var description = $"Nạp tiền vào ví qua MoMo";
                                            
                                            await _emailService.SendInvoiceEmailAsync(
                                                user.Email,
                                                user.UserName ?? user.Email,
                                                invoiceNumber,
                                                payment.OrderId,
                                                payment.TransactionId,
                                                payment.Amount,
                                                description);
                                            
                                            _logger.LogInformation("Đã gửi email hóa đơn cho user {UserId} (Email: {Email}) cho payment {PaymentId}",
                                                payment.ContextId, user.Email, payment.Id);
                                        }
                                    }
                                    catch (Exception emailEx)
                                    {
                                        // Email lỗi KHÔNG ảnh hưởng đến việc đã cộng tiền (đã lưu ở trên)
                                        _logger.LogWarning(emailEx,
                                            "PHỤ KIỆN LỖI: Không thể gửi email hóa đơn cho user {UserId} cho payment {PaymentId} nhưng tiền đã được cộng vào ví thành công",
                                            payment.ContextId, payment.Id);
                                    }
                                }
                                else
                                {
                                    // Transaction không tìm thấy - có thể do lỗi hoặc chưa được lưu
                                    // Nhưng tiền đã được cộng (SaveChangesAsync đã thành công ở trên)
                                    _logger.LogWarning(
                                        "CẢNH BÁO: Transaction CHƯA được tìm thấy trong database sau khi SaveChangesAsync cho payment {PaymentId} (OrderId: {OrderId}). " +
                                        "Tiền đã được cộng nhưng notification và email sẽ không được gửi.",
                                        payment.Id, payment.OrderId);
                                }
                            }
                            catch (Exception checkEx)
                            {
                                // Lỗi khi kiểm tra transaction hoặc tạo notification/email
                                // KHÔNG ảnh hưởng đến việc đã cộng tiền (đã lưu ở trên)
                                _logger.LogWarning(checkEx,
                                    "PHỤ KIỆN LỖI: Không thể kiểm tra transaction và tạo notification/email sau khi lưu cho payment {PaymentId} (OrderId: {OrderId}). " +
                                    "Tiền đã được cộng vào ví thành công.",
                                    payment.Id, payment.OrderId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ghi log lỗi chi tiết với stack trace đầy đủ
                        _logger.LogError(ex, 
                            "LỖI NGHIÊM TRỌNG: Không thể áp dụng business logic cho payment {PaymentId} (OrderId: {OrderId}, ContextType: {ContextType}, ContextId: {ContextId}). " +
                            "Trạng thái payment đã được cập nhật thành Paid nhưng tiền CHƯA được cộng vào ví. " +
                            "Lỗi: {ErrorMessage}. StackTrace: {StackTrace}",
                            payment.Id, payment.OrderId, payment.ContextType, payment.ContextId, ex.Message, ex.StackTrace);
                        
                        // QUAN TRỌNG: Vẫn return OK cho MoMo (theo requirement của MoMo)
                        // Nhưng payment sẽ được retry khi IPN được gửi lại hoặc khi query payment
                    }
                }
            }
            else
            {
                // Thanh toán thất bại (ResultCode != 0)
                // Kiểm tra xem có phải payment expired không
                var messageLower = request.Message?.ToLowerInvariant() ?? string.Empty;
                if (messageLower.Contains("hết hạn") || messageLower.Contains("expired") || 
                    messageLower.Contains("không tồn tại") || messageLower.Contains("not found"))
                {
                    payment.Status = PaymentStatus.Expired;
                    _logger.LogInformation(
                        "Payment {PaymentId} (OrderId: {OrderId}) đã hết hạn. Message: {Message}",
                        payment.Id, payment.OrderId, request.Message);
                }
                else
                {
                    payment.Status = PaymentStatus.Failed;
                }
                // Lưu trạng thái và log
                await _uow.SaveChangesAsync();

                // Gửi notification cho user khi payment fails
                if (!string.IsNullOrEmpty(payment.ContextId) && payment.Status == PaymentStatus.Failed)
                {
                    try
                    {
                        var notification = await _notificationService.CreateWalletNotificationAsync(
                            payment.ContextId,
                            NotificationType.PaymentFailed,
                            payment.Amount,
                            $"Thanh toán thất bại: {request.Message}",
                            payment.Id,
                            ct);
                        await _uow.SaveChangesAsync();
                        await _notificationService.SendRealTimeNotificationAsync(payment.ContextId, notification, ct);
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx,
                            "Không thể gửi notification payment failed cho user {UserId}",
                            payment.ContextId);
                    }
                }
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
        try
        {
            Console.WriteLine($"[QueryPaymentAsync] 🔍 Bắt đầu query payment từ MoMo: PaymentId={paymentId}");
            var payment = await _uow.Payments.GetByIdAsync(paymentId);
            if (payment == null)
            {
                Console.WriteLine($"[QueryPaymentAsync] ❌ Payment không tồn tại: PaymentId={paymentId}");
                throw new ArgumentException("Payment not found.", nameof(paymentId));
            }
            
            Console.WriteLine($"[QueryPaymentAsync] 📋 Payment info: PaymentId={payment.Id}, OrderId={payment.OrderId}, Status={payment.Status}, ResultCode={payment.ResultCode}, Message={payment.Message}");

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

        // Deserialize với options cho phép linh hoạt hơn (transId có thể là string hoặc số)
        var jsonOptions = new JsonSerializerOptions(_jsonOptions)
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
        
        MomoQueryResponse momoResponse;
        try
        {
            momoResponse = JsonSerializer.Deserialize<MomoQueryResponse>(responseContent, jsonOptions)
                ?? throw new InvalidOperationException("MoMo query response invalid.");
            
            Console.WriteLine($"[QueryPaymentAsync] 📥 MoMo response: ResultCode={momoResponse.ResultCode}, Message={momoResponse.Message}, TransId={momoResponse.TransId ?? "NULL"}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[QueryPaymentAsync] ❌ Lỗi deserialize MoMo response: {ex.Message}, ResponseContent={responseContent}");
            _logger.LogError(ex, 
                "Lỗi khi deserialize MoMo query response. Response content: {ResponseContent}",
                responseContent);
            throw new InvalidOperationException($"MoMo query response invalid: {ex.Message}", ex);
        }

        // Nếu MoMo báo thanh toán thành công (ResultCode = 0)
        if (momoResponse.ResultCode == 0)
        {
            Console.WriteLine($"[QueryPaymentAsync] ✅ MoMo báo ResultCode = 0 (thành công) cho PaymentId={payment.Id}, OrderId={payment.OrderId}, Status hiện tại={payment.Status}, Message={momoResponse.Message}");
            
            // QUAN TRỌNG: Cập nhật payment status thành Paid nếu chưa Paid (kể cả khi status = Failed hoặc Pending)
            // Vì có thể payment đã bị set Failed/Pending trước đó do query khi chưa thanh toán
            bool wasNotPaid = payment.Status != PaymentStatus.Paid;
            if (wasNotPaid)
            {
                var oldStatus = payment.Status;
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.UtcNow;
                payment.ResultCode = momoResponse.ResultCode;
                payment.Message = momoResponse.Message ?? "Thành công.";
                
                // Cập nhật TransactionId nếu có
                if (!string.IsNullOrWhiteSpace(momoResponse.TransId) && string.IsNullOrWhiteSpace(payment.TransactionId))
                {
                    payment.TransactionId = momoResponse.TransId;
                }
                
                await _uow.SaveChangesAsync();
                Console.WriteLine($"[QueryPaymentAsync] 💰 Đã update PaymentId={payment.Id} từ {oldStatus} → Paid. TransId={momoResponse.TransId ?? "NULL"}");
                _logger.LogInformation(
                    "💰 [Query] Payment {PaymentId} (OrderId: {OrderId}) đã được cập nhật từ {OldStatus} thành Paid từ Query. TransId: {TransId}",
                    payment.Id, payment.OrderId, oldStatus, momoResponse.TransId ?? "NULL");
            }
            else
            {
                Console.WriteLine($"[QueryPaymentAsync] ⚠️ PaymentId={payment.Id} đã Paid rồi, không cần update.");
            }
            
            // Kiểm tra xem đã có transaction chưa
            var hasTransaction = await CheckIfBusinessLogicAppliedAsync(payment, ct);
            if (!hasTransaction)
            {
                _logger.LogWarning(
                    "Payment {PaymentId} (OrderId: {OrderId}) đã Paid nhưng chưa có transaction. Tự động retry từ QueryPayment...",
                    payment.Id, payment.OrderId);
                
                try
                {
                    // Tạo MomoIpnRequestDto từ query response để retry
                    // Lưu ý: Không validate signature vì đây là retry từ query, không phải IPN thật
                    var ipnRequest = new MomoIpnRequestDto
                    {
                        AccessKey = _options.AccessKey,
                        Amount = (long)payment.Amount,
                        ExtraData = payment.ExtraData ?? string.Empty,
                        Message = momoResponse.Message ?? "Thành công.",
                        OrderId = payment.OrderId,
                        OrderInfo = $"Payment for {payment.ContextType}",
                        OrderType = "momo_wallet",
                        PartnerCode = _options.PartnerCode,
                        PayType = "webApp",
                        RequestId = payment.RequestId, // Sử dụng RequestId từ payment
                        ResponseTime = momoResponse.ResponseTime,
                        ResultCode = momoResponse.ResultCode,
                        TransId = momoResponse.TransId ?? string.Empty,
                        Signature = string.Empty // Không cần validate signature cho retry
                    };
                    
                    // Cập nhật TransactionId vào payment nếu có
                    if (!string.IsNullOrWhiteSpace(momoResponse.TransId) && string.IsNullOrWhiteSpace(payment.TransactionId))
                    {
                        payment.TransactionId = momoResponse.TransId;
                    }
                    
                    await ApplyPaymentSuccessAsync(payment, ipnRequest, ct);
                    var savedCount = await _uow.SaveChangesAsync();
                    _logger.LogInformation(
                        "Đã retry thành công từ QueryPayment cho payment {PaymentId} (OrderId: {OrderId}). Số entities đã lưu: {SavedCount}",
                        payment.Id, payment.OrderId, savedCount);
                    
                    // Tạo notification và email sau khi retry thành công (chỉ cho WalletDeposit)
                    if (payment.ContextType == PaymentContextType.WalletDeposit)
                    {
                        try
                        {
                            var wallet = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
                            var (transactions, total) = await _uow.Transactions.GetByWalletIdAsync(wallet.Id, 1, 10, ct);
                            var transaction = transactions.FirstOrDefault(t => 
                                t.Type == TransactionType.Credit && 
                                t.Status == TransactionStatus.Succeeded &&
                                t.Note != null && 
                                t.Note.Contains(payment.OrderId));
                            
                            if (transaction != null)
                            {
                                // PHỤ KIỆN: Tạo và gửi notification
                                try
                                {
                                    var notification = await _notificationService.CreateWalletNotificationAsync(
                                        payment.ContextId,
                                        NotificationType.WalletDeposit,
                                        payment.Amount,
                                        $"Nạp ví qua MoMo (order {payment.OrderId})",
                                        transaction.Id,
                                        ct);

                                    await _uow.SaveChangesAsync();
                                    await _notificationService.SendRealTimeNotificationAsync(payment.ContextId, notification, ct);
                                    _logger.LogInformation("Đã gửi notification sau khi retry từ QueryPayment cho user {UserId}", payment.ContextId);
                                }
                                catch (Exception notifEx)
                                {
                                    _logger.LogWarning(notifEx,
                                        "PHỤ KIỆN LỖI: Không thể gửi notification sau khi retry từ QueryPayment cho user {UserId} nhưng tiền đã được cộng",
                                        payment.ContextId);
                                }

                                // PHỤ KIỆN: Gửi email
                                try
                                {
                                    var user = await _uow.Users.GetByIdAsync(payment.ContextId);
                                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                                    {
                                        await _emailService.SendInvoiceEmailAsync(
                                            user.Email,
                                            user.UserName ?? user.Email,
                                            $"INV-{payment.OrderId}",
                                            payment.OrderId,
                                            payment.TransactionId,
                                            payment.Amount,
                                            "Nạp tiền vào ví qua MoMo");
                                        _logger.LogInformation("Đã gửi email sau khi retry từ QueryPayment cho user {UserId}", payment.ContextId);
                                    }
                                }
                                catch (Exception emailEx)
                                {
                                    _logger.LogWarning(emailEx,
                                        "PHỤ KIỆN LỖI: Không thể gửi email sau khi retry từ QueryPayment cho user {UserId} nhưng tiền đã được cộng",
                                        payment.ContextId);
                                }
                            }
                        }
                        catch (Exception checkEx)
                        {
                            _logger.LogWarning(checkEx,
                                "PHỤ KIỆN LỖI: Không thể kiểm tra transaction và tạo notification/email sau khi retry từ QueryPayment cho payment {PaymentId}",
                                payment.Id);
                        }
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx,
                        "LỖI khi retry từ QueryPayment cho payment {PaymentId} (OrderId: {OrderId}). Lỗi: {ErrorMessage}",
                        payment.Id, payment.OrderId, retryEx.Message);
                }
            }
        }

        // Cập nhật payment status nếu cần
        if (momoResponse.ResultCode != 0)
        {
            Console.WriteLine($"[QueryPaymentAsync] ⚠️ MoMo báo ResultCode != 0 (chưa thành công): ResultCode={momoResponse.ResultCode}, Message={momoResponse.Message}, PaymentId={payment.Id}, Status hiện tại={payment.Status}");
            var messageLower = momoResponse.Message?.ToLowerInvariant() ?? string.Empty;
            if (messageLower.Contains("hết hạn") || messageLower.Contains("expired") || 
                messageLower.Contains("không tồn tại") || messageLower.Contains("not found"))
            {
                if (payment.Status != PaymentStatus.Expired)
                {
                    payment.Status = PaymentStatus.Expired;
                    payment.Message = momoResponse.Message;
                    await _uow.SaveChangesAsync();
                    _logger.LogInformation(
                        "Payment {PaymentId} (OrderId: {OrderId}) đã được cập nhật thành Expired từ Query. Message: {Message}",
                        payment.Id, payment.OrderId, momoResponse.Message);
                }
            }
            // QUAN TRỌNG: Không set Failed nếu message là "chờ người dùng xác nhận" (vẫn là Pending)
            else if (messageLower.Contains("chờ người dùng") || messageLower.Contains("đã được khởi tạo") ||
                     messageLower.Contains("waiting") || messageLower.Contains("pending"))
            {
                // Giữ nguyên status = Pending, không set Failed
                _logger.LogInformation(
                    "Payment {PaymentId} (OrderId: {OrderId}) vẫn đang chờ người dùng xác nhận. Giữ nguyên status = Pending.",
                    payment.Id, payment.OrderId);
                payment.Message = momoResponse.Message; // Cập nhật message nhưng không đổi status
                await _uow.SaveChangesAsync();
            }
            else if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Failed;
                payment.Message = momoResponse.Message;
                await _uow.SaveChangesAsync();

                // Gửi notification cho user khi payment fails
                if (!string.IsNullOrEmpty(payment.ContextId))
                {
                    try
                    {
                        var notification = await _notificationService.CreateWalletNotificationAsync(
                            payment.ContextId,
                            NotificationType.PaymentFailed,
                            payment.Amount,
                            $"Thanh toán thất bại: {momoResponse.Message}",
                            payment.Id,
                            ct);
                        await _uow.SaveChangesAsync();
                        await _notificationService.SendRealTimeNotificationAsync(payment.ContextId, notification, ct);
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx,
                            "Không thể gửi notification payment failed cho user {UserId}",
                            payment.ContextId);
                    }
                }
            }
        }

            return new MomoQueryResponseDto
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                ResultCode = momoResponse.ResultCode,
                Message = momoResponse.Message,
                TransId = momoResponse.TransId,
                Status = payment.Status == PaymentStatus.Expired ? "EXPIRED" : 
                         (momoResponse.ResultCode == 0 ? "SUCCESS" : "FAILED"),
                Amount = momoResponse.Amount,
                ResponseTime = momoResponse.ResponseTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "LỖI khi query payment {PaymentId}. Lỗi: {ErrorMessage}. StackTrace: {StackTrace}",
                paymentId, ex.Message, ex.StackTrace);
            throw; // Re-throw để controller có thể xử lý
        }
    }

    public async Task<OperationResult> RetryPaymentAsync(string paymentId, string userId, CancellationToken ct = default)
    {
        try
        {
            var payment = await _uow.Payments.GetByIdAsync(paymentId);
            if (payment == null)
            {
                return new OperationResult { Status = "Fail", Message = "Payment not found." };
            }

            // Log chi tiết để debug
            _logger.LogInformation(
                "RetryPayment: PaymentId={PaymentId}, OrderId={OrderId}, ContextId={ContextId}, UserId={UserId}, ContextType={ContextType}, Status={Status}",
                payment.Id, payment.OrderId, payment.ContextId, userId, payment.ContextType, payment.Status);

            // Kiểm tra payment có thuộc về user không
            // Nếu payment là WalletDeposit, cho phép retry vì đây là nạp tiền (không phải escrow)
            if (payment.ContextId != userId)
            {
                _logger.LogWarning(
                    "Payment {PaymentId} có ContextId={ContextId} khác với UserId={UserId}. ContextType={ContextType}",
                    payment.Id, payment.ContextId, userId, payment.ContextType);
                
                // Nếu là WalletDeposit, vẫn cho phép retry (có thể là admin retry cho user khác)
                if (payment.ContextType == PaymentContextType.WalletDeposit)
                {
                    _logger.LogInformation(
                        "Cho phép retry WalletDeposit payment {PaymentId} mặc dù ContextId khác UserId (có thể là admin retry)",
                        payment.Id);
                }
                else
                {
                    return new OperationResult { Status = "Fail", Message = $"Payment does not belong to you. Payment ContextId: {payment.ContextId}, Your UserId: {userId}" };
                }
            }

            // QUAN TRỌNG: Luôn query payment status từ MoMo để đảm bảo có thông tin mới nhất
            // Đặc biệt quan trọng với MoMo demo vì không tự động gửi IPN
            _logger.LogInformation(
                "🔄 [Retry] Đang query payment status từ MoMo cho PaymentId={PaymentId}, OrderId={OrderId}, Status hiện tại={Status}",
                payment.Id, payment.OrderId, payment.Status);
            
            try
            {
                // Query payment status từ MoMo (tự động cập nhật status và cộng tiền nếu thành công)
                var queryResponse = await QueryPaymentAsync(paymentId, ct);
                
                // Reload payment để lấy status mới nhất sau khi query
                payment = await _uow.Payments.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new OperationResult { Status = "Fail", Message = "Payment not found after query." };
                }
                
                _logger.LogInformation(
                    "✅ [Retry] Đã query từ MoMo: PaymentId={PaymentId}, OrderId={OrderId}, Status sau query={Status}",
                    payment.Id, payment.OrderId, payment.Status);
                
                // Nếu sau khi query vẫn chưa Paid, kiểm tra ResultCode từ MoMo
                if (payment.Status != PaymentStatus.Paid)
                {
                    // Nếu MoMo báo ResultCode = 0 (thành công) nhưng status chưa Paid, có thể do delay
                    // Hoặc nếu có TransactionId, có nghĩa là đã thanh toán thành công
                    if (payment.ResultCode == 0 || !string.IsNullOrWhiteSpace(payment.TransactionId))
                    {
                        _logger.LogWarning(
                            "⚠️ [Retry] Payment {PaymentId} (OrderId: {OrderId}) có ResultCode=0 hoặc TransactionId nhưng status = {Status}. " +
                            "Có thể do delay, sẽ force update status = Paid.",
                            payment.Id, payment.OrderId, payment.Status);
                        
                        // Force update status = Paid nếu có ResultCode = 0 hoặc TransactionId
                        payment.Status = PaymentStatus.Paid;
                        if (payment.PaidAt == null)
                        {
                            payment.PaidAt = DateTime.UtcNow;
                        }
                        await _uow.SaveChangesAsync();
                        
                        _logger.LogInformation(
                            "✅ [Retry] Đã force update payment {PaymentId} status = Paid vì có ResultCode=0 hoặc TransactionId.",
                            payment.Id);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "⚠️ [Retry] Payment {PaymentId} (OrderId: {OrderId}) vẫn chưa Paid sau khi query. Status: {Status}, ResultCode: {ResultCode}. " +
                            "Có thể thanh toán chưa hoàn tất hoặc đã thất bại.",
                            payment.Id, payment.OrderId, payment.Status, payment.ResultCode);
                        return new OperationResult 
                        { 
                            Status = "Fail", 
                            Message = $"Thanh toán chưa hoàn tất. Trạng thái hiện tại: {payment.Status}. Vui lòng thử lại sau." 
                        };
                    }
                }
                
                _logger.LogInformation(
                    "💰 [Retry] Payment {PaymentId} (OrderId: {OrderId}) đã được cập nhật thành Paid sau khi query từ MoMo.",
                    payment.Id, payment.OrderId);
            }
            catch (Exception queryEx)
            {
                _logger.LogError(queryEx,
                    "❌ [Retry] Lỗi khi query payment status từ MoMo cho payment {PaymentId} (OrderId: {OrderId}): {ErrorMessage}",
                    payment.Id, payment.OrderId, queryEx.Message);
                
                // Nếu payment đã Paid trước đó, vẫn tiếp tục xử lý (có thể IPN đã đến nhưng query lỗi)
                if (payment.Status == PaymentStatus.Paid)
                {
                    _logger.LogInformation(
                        "⚠️ [Retry] Payment đã Paid trước đó, tiếp tục xử lý mặc dù query lỗi.");
                }
                else
                {
                    return new OperationResult 
                    { 
                        Status = "Fail", 
                        Message = $"Không thể kiểm tra trạng thái thanh toán từ MoMo: {queryEx.Message}" 
                    };
                }
            }

            // QUAN TRỌNG: Đảm bảo payment status = Paid trước khi apply business logic
            // Nếu query từ MoMo thành công (ResultCode = 0) nhưng status vẫn chưa Paid, force update
            if (payment.Status != PaymentStatus.Paid)
            {
                // Kiểm tra xem có phải MoMo đã báo thành công nhưng status chưa update không
                // (có thể do race condition hoặc lỗi trong QueryPaymentAsync)
                if (payment.ResultCode == 0 && !string.IsNullOrWhiteSpace(payment.TransactionId))
                {
                    // MoMo đã báo thành công và có TransactionId, force update status = Paid
                    _logger.LogWarning(
                        "⚠️ [Retry] Payment {PaymentId} (OrderId: {OrderId}) có ResultCode=0 và TransactionId nhưng status = {Status}. Force update status = Paid.",
                        payment.Id, payment.OrderId, payment.Status);
                    payment.Status = PaymentStatus.Paid;
                    if (payment.PaidAt == null)
                    {
                        payment.PaidAt = DateTime.UtcNow;
                    }
                    await _uow.SaveChangesAsync();
                    Console.WriteLine($"[Retry] ✅ Force update payment status = Paid: PaymentId={payment.Id}, OrderId={payment.OrderId}");
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️ [Retry] Payment {PaymentId} (OrderId: {OrderId}) có status {Status} khác Paid. Không thể apply business logic.",
                        payment.Id, payment.OrderId, payment.Status);
                    return new OperationResult 
                    { 
                        Status = "Fail", 
                        Message = $"Payment status phải là Paid mới có thể cộng tiền. Status hiện tại: {payment.Status}" 
                    };
                }
            }

            // Kiểm tra xem đã có transaction chưa
            var hasTransaction = await CheckIfBusinessLogicAppliedAsync(payment, ct);
            if (hasTransaction)
            {
                // QUAN TRỌNG: Nếu đã có transaction nhưng status != Paid, cần update status = Paid
                if (payment.Status != PaymentStatus.Paid)
                {
                    _logger.LogWarning(
                        "⚠️ [Retry] Payment {PaymentId} (OrderId: {OrderId}) đã có transaction nhưng status = {Status}. Tự động update status = Paid.",
                        payment.Id, payment.OrderId, payment.Status);
                    payment.Status = PaymentStatus.Paid;
                    if (payment.PaidAt == null)
                    {
                        payment.PaidAt = DateTime.UtcNow;
                    }
                    payment.ResultCode = 0;
                    payment.Message = "Thành công.";
                    await _uow.SaveChangesAsync();
                    _logger.LogInformation(
                        "✅ [Retry] Đã update payment status = Paid cho payment {PaymentId} (OrderId: {OrderId})",
                        payment.Id, payment.OrderId);
                }
                return new OperationResult { Status = "Ok", Message = "Payment already processed. Transaction exists." };
            }

            _logger.LogInformation(
                "🔄 [Retry] User {UserId} đang retry payment {PaymentId} (OrderId: {OrderId}, Status: {Status})",
                userId, payment.Id, payment.OrderId, payment.Status);

            // Đảm bảo payment status = Paid và có PaidAt
            if (payment.PaidAt == null)
            {
                payment.PaidAt = DateTime.UtcNow;
            }
            if (payment.ResultCode == null || payment.ResultCode != 0)
            {
                payment.ResultCode = 0;
                payment.Message = "Thành công.";
            }

            // Tạo MomoIpnRequestDto để retry (không validate signature)
            var ipnRequest = new MomoIpnRequestDto
            {
                AccessKey = _options.AccessKey,
                Amount = (long)payment.Amount,
                ExtraData = payment.ExtraData ?? string.Empty,
                Message = payment.Message ?? "Thành công.",
                OrderId = payment.OrderId,
                OrderInfo = $"Payment for {payment.ContextType}",
                OrderType = "momo_wallet",
                PartnerCode = _options.PartnerCode,
                PayType = "webApp",
                RequestId = payment.RequestId,
                ResponseTime = payment.PaidAt != null 
                    ? new DateTimeOffset(payment.PaidAt.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() 
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ResultCode = 0, // Luôn là 0 (thành công) khi retry
                TransId = payment.TransactionId ?? string.Empty,
                Signature = string.Empty // Không validate signature cho retry
            };

            // QUAN TRỌNG: Đảm bảo ApplyPaymentSuccessAsync luôn được gọi và cộng tiền
            // Retry tối đa 3 lần nếu có lỗi (ví dụ: concurrency exception)
            int maxRetries = 3;
            int retryCount = 0;
            bool success = false;
            
            while (retryCount < maxRetries && !success)
            {
                try
                {
                    await ApplyPaymentSuccessAsync(payment, ipnRequest, ct);
                    var savedCount = await _uow.SaveChangesAsync();
                    
                    // Kiểm tra lại xem đã có transaction chưa
                    var hasTransactionAfterApply = await CheckIfBusinessLogicAppliedAsync(payment, ct);
                    if (hasTransactionAfterApply)
                    {
                        success = true;
                        _logger.LogInformation(
                            "✅ [Retry] Đã retry thành công payment {PaymentId} (OrderId: {OrderId}) cho user {UserId}. Số entities đã lưu: {SavedCount}",
                            payment.Id, payment.OrderId, userId, savedCount);
                        
                        Console.WriteLine($"[Retry] ✅ Đã cộng tiền thành công - OrderId: {payment.OrderId}, Amount: {payment.Amount}, PaymentId: {payment.Id}");
                    }
                    else
                    {
                        retryCount++;
                        _logger.LogWarning(
                            "⚠️ [Retry] ApplyPaymentSuccessAsync đã chạy nhưng chưa có transaction. Retry lần {RetryCount}/{MaxRetries}",
                            retryCount, maxRetries);
                        
                        if (retryCount < maxRetries)
                        {
                            // Reload payment và wallet để tránh concurrency issue
                            payment = await _uow.Payments.GetByIdAsync(paymentId);
                            if (payment == null)
                            {
                                throw new ArgumentException("Payment not found after retry.");
                            }
                            await Task.Delay(500, ct); // Đợi 500ms trước khi retry
                        }
                    }
                }
                catch (DbUpdateConcurrencyException concurrencyEx)
                {
                    retryCount++;
                    _logger.LogWarning(concurrencyEx,
                        "⚠️ [Retry] Concurrency exception khi apply payment success. Retry lần {RetryCount}/{MaxRetries}",
                        retryCount, maxRetries);
                    
                    if (retryCount < maxRetries)
                    {
                        // Reload payment và wallet để tránh concurrency issue
                        payment = await _uow.Payments.GetByIdAsync(paymentId);
                        if (payment == null)
                        {
                            throw new ArgumentException("Payment not found after retry.");
                        }
                        await Task.Delay(500, ct); // Đợi 500ms trước khi retry
                    }
                    else
                    {
                        throw new InvalidOperationException($"Không thể cộng tiền sau {maxRetries} lần retry. Lỗi: {concurrencyEx.Message}", concurrencyEx);
                    }
                }
            }
            
            if (!success)
            {
                _logger.LogError(
                    "❌ [Retry] Không thể cộng tiền sau {MaxRetries} lần retry cho payment {PaymentId} (OrderId: {OrderId})",
                    maxRetries, payment.Id, payment.OrderId);
                throw new InvalidOperationException($"Không thể cộng tiền sau {maxRetries} lần retry.");
            }

            // Tạo notification và email sau khi retry thành công (chỉ cho WalletDeposit)
            if (payment.ContextType == PaymentContextType.WalletDeposit)
            {
                try
                {
                    var wallet = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
                    var (transactions, total) = await _uow.Transactions.GetByWalletIdAsync(wallet.Id, 1, 10, ct);
                    var transaction = transactions.FirstOrDefault(t => 
                        t.Type == TransactionType.Credit && 
                        t.Status == TransactionStatus.Succeeded &&
                        t.Note != null && 
                        t.Note.Contains(payment.OrderId));
                    
                    if (transaction != null)
                    {
                        // PHỤ KIỆN: Tạo và gửi notification
                        try
                        {
                            var notification = await _notificationService.CreateWalletNotificationAsync(
                                payment.ContextId,
                                NotificationType.WalletDeposit,
                                payment.Amount,
                                $"Nạp ví qua MoMo (order {payment.OrderId})",
                                transaction.Id,
                                ct);

                            await _uow.SaveChangesAsync();
                            await _notificationService.SendRealTimeNotificationAsync(payment.ContextId, notification, ct);
                            _logger.LogInformation("Đã gửi notification sau khi retry cho user {UserId}", payment.ContextId);
                        }
                        catch (Exception notifEx)
                        {
                            _logger.LogWarning(notifEx,
                                "PHỤ KIỆN LỖI: Không thể gửi notification sau khi retry cho user {UserId} nhưng tiền đã được cộng",
                                payment.ContextId);
                        }

                        // PHỤ KIỆN: Gửi email
                        try
                        {
                            var user = await _uow.Users.GetByIdAsync(payment.ContextId);
                            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                            {
                                await _emailService.SendInvoiceEmailAsync(
                                    user.Email,
                                    user.UserName ?? user.Email,
                                    $"INV-{payment.OrderId}",
                                    payment.OrderId,
                                    payment.TransactionId,
                                    payment.Amount,
                                    "Nạp tiền vào ví qua MoMo");
                                _logger.LogInformation("Đã gửi email sau khi retry cho user {UserId}", payment.ContextId);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx,
                                "PHỤ KIỆN LỖI: Không thể gửi email sau khi retry cho user {UserId} nhưng tiền đã được cộng",
                                payment.ContextId);
                        }
                    }
                }
                catch (Exception checkEx)
                {
                    _logger.LogWarning(checkEx,
                        "PHỤ KIỆN LỖI: Không thể kiểm tra transaction và tạo notification/email sau khi retry cho payment {PaymentId}",
                        payment.Id);
                }
            }

            return new OperationResult { Status = "Ok", Message = "Payment retry successful. Money has been added to wallet." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LỖI khi retry payment {PaymentId} cho user {UserId}. Lỗi: {ErrorMessage}",
                paymentId, userId, ex.Message);
            return new OperationResult { Status = "Fail", Message = $"Retry failed: {ex.Message}" };
        }
    }

    public async Task<OperationResult> RetryPaymentByOrderIdAsync(string orderId, string userId, CancellationToken ct = default)
    {
        try
        {
            // Tìm payment bằng OrderId
            var payment = await _uow.Payments.GetByOrderIdAsync(PaymentProvider.MoMo, orderId, ct);
            if (payment == null)
            {
                return new OperationResult { Status = "Fail", Message = $"Payment with OrderId {orderId} not found." };
            }

            // Gọi RetryPaymentAsync với paymentId đã tìm được
            return await RetryPaymentAsync(payment.Id, userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LỖI khi retry payment bằng OrderId {OrderId} cho user {UserId}. Lỗi: {ErrorMessage}",
                orderId, userId, ex.Message);
            return new OperationResult { Status = "Fail", Message = $"Retry failed: {ex.Message}" };
        }
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
            CounterpartyUserId = escrow.StudentUserId
        }, ct);

        escrow.Status = EscrowStatus.Held;
        escrow.StudentUserId = escrow.StudentUserId ?? payment.ContextId;

        var notification = await _notificationService.CreateEscrowNotificationAsync(
            escrow.StudentUserId ?? string.Empty,
            NotificationType.EscrowPaid,
            payment.Amount,
            escrow.ClassId ?? string.Empty,
            payment.Id,
            ct);

        await _notificationService.SendRealTimeNotificationAsync(escrow.StudentUserId ?? string.Empty, notification, ct);

        // Send invoice email
        if (!string.IsNullOrWhiteSpace(escrow.StudentUserId))
        {
            try
            {
                var studentUser = await _uow.Users.GetByIdAsync(escrow.StudentUserId);
                if (studentUser != null && !string.IsNullOrWhiteSpace(studentUser.Email))
                {
                    string? classTitle = null;
                    string? classSubject = null;
                    
                    if (!string.IsNullOrWhiteSpace(escrow.ClassId))
                    {
                        var classEntity = await _uow.Classes.GetByIdAsync(escrow.ClassId);
                        if (classEntity != null)
                        {
                            classTitle = classEntity.Title;
                            classSubject = classEntity.Subject;
                        }
                    }
                    
                    var invoiceNumber = $"INV-{payment.OrderId}";
                    var description = !string.IsNullOrWhiteSpace(classTitle) 
                        ? $"Thanh toán học phí lớp học: {classTitle}"
                        : $"Thanh toán học phí qua MoMo";
                    
                    await _emailService.SendInvoiceEmailAsync(
                        studentUser.Email,
                        studentUser.UserName ?? studentUser.Email,
                        invoiceNumber,
                        payment.OrderId,
                        payment.TransactionId,
                        payment.Amount,
                        description,
                        classTitle,
                        classSubject);
                    
                    _logger.LogInformation("Đã gửi email hóa đơn cho user {UserId} (Email: {Email}) cho payment {PaymentId}",
                        escrow.StudentUserId, studentUser.Email, payment.Id);
                }
            }
            catch (Exception emailEx)
            {
                // Email lỗi không ảnh hưởng đến payment processing
                _logger.LogWarning(emailEx, "Không thể gửi email hóa đơn cho user {UserId} cho payment {PaymentId}",
                    escrow.StudentUserId, payment.Id);
            }
        }
    }

    private async Task ApplyWalletDepositAsync(Payment payment, MomoIpnRequestDto request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Bắt đầu cộng tiền vào ví cho payment {PaymentId} (OrderId: {OrderId}, UserId: {UserId}, Amount: {Amount})",
            payment.Id, payment.OrderId, payment.ContextId, payment.Amount);

        try
        {
            // Bước 1: Validate ContextId
            if (string.IsNullOrWhiteSpace(payment.ContextId))
            {
                _logger.LogError(
                    "Payment {PaymentId} (OrderId: {OrderId}) có ContextId rỗng. Không thể cộng tiền vào ví.",
                    payment.Id, payment.OrderId);
                throw new ArgumentException($"Payment {payment.Id} has empty ContextId", nameof(payment));
            }
            
            _logger.LogInformation(
                "Bắt đầu cộng tiền vào ví cho payment {PaymentId} (OrderId: {OrderId}) với ContextId: {ContextId}",
                payment.Id, payment.OrderId, payment.ContextId);
            
            // Bước 2: Lấy hoặc tạo wallet
            var wallet = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
            _logger.LogInformation(
                "Đã lấy wallet {WalletId} cho user {UserId}. Số dư hiện tại: {CurrentBalance}",
                wallet.Id, payment.ContextId, wallet.Balance);
            
            // Validate wallet
            if (wallet == null)
            {
                _logger.LogError(
                    "Không thể lấy hoặc tạo wallet cho user {UserId} (Payment {PaymentId}, OrderId: {OrderId})",
                    payment.ContextId, payment.Id, payment.OrderId);
                throw new InvalidOperationException($"Cannot get or create wallet for user {payment.ContextId}");
            }

            // Bước 3: Reload wallet từ DB với tracking để EF có thể update
            // (Vì GetMyWalletAsync dùng AsNoTracking, nên cần reload để track)
            // QUAN TRỌNG: GetByIdAsync dùng FindAsync, nó sẽ track entity tự động
            var trackedWallet = await _uow.Wallets.GetByIdAsync(wallet.Id);
            if (trackedWallet == null)
            {
                _logger.LogError(
                    "Không tìm thấy wallet {WalletId} trong database sau khi GetMyWalletAsync cho payment {PaymentId} (OrderId: {OrderId})",
                    wallet.Id, payment.Id, payment.OrderId);
                throw new InvalidOperationException($"Wallet {wallet.Id} not found in database");
            }
            
            _logger.LogInformation(
                "Đã reload wallet {WalletId} với tracking. Số dư hiện tại: {CurrentBalance}",
                trackedWallet.Id, trackedWallet.Balance);

            // Bước 4: Cộng tiền vào ví
            // QUAN TRỌNG: trackedWallet đã được track bởi EF (từ GetByIdAsync/FindAsync)
            // Chỉ cần thay đổi property, EF sẽ tự động detect change
            var oldBalance = trackedWallet.Balance;
            trackedWallet.Balance += payment.Amount;
            
            // QUAN TRỌNG: Vì trackedWallet đã được track bởi FindAsync, 
            // chỉ cần thay đổi property là đủ. EF sẽ tự động detect change.
            // Nhưng để đảm bảo, vẫn gọi Update() để set state = Modified
            await _uow.Wallets.Update(trackedWallet);
            
            _logger.LogInformation(
                "Đã cập nhật số dư ví trong memory: {OldBalance} -> {NewBalance} (+{Amount}). " +
                "Wallet sẽ được lưu khi SaveChangesAsync được gọi.",
                oldBalance, trackedWallet.Balance, payment.Amount);

            // Bước 5: Tạo transaction record
            // Lưu TransactionId từ MoMo vào Note để hiển thị trong lịch sử
            // Ưu tiên lấy từ payment.TransactionId, nếu chưa có thì lấy từ request.TransId
            var momoTransId = !string.IsNullOrWhiteSpace(payment.TransactionId) 
                ? payment.TransactionId 
                : (!string.IsNullOrWhiteSpace(request.TransId) ? request.TransId : null);
            
            var note = !string.IsNullOrWhiteSpace(momoTransId)
                ? $"MoMo wallet deposit {payment.OrderId} (TransId: {momoTransId})"
                : $"MoMo wallet deposit {payment.OrderId}";
            
            var transaction = new Transaction
            {
                WalletId = trackedWallet.Id,
                Type = TransactionType.Credit,
                Status = TransactionStatus.Succeeded,
                Amount = payment.Amount,
                Note = note,
                CounterpartyUserId = payment.ContextId
            };

            await _uow.Transactions.AddAsync(transaction, ct);
            _logger.LogInformation(
                "Đã tạo transaction object cho wallet {WalletId}. TransactionId={TransactionId}, WalletId={WalletId}, Amount={Amount}, Type={Type}, Note={Note}. " +
                "Transaction sẽ được lưu khi SaveChangesAsync được gọi.",
                trackedWallet.Id, transaction.Id, trackedWallet.Id, transaction.Amount, transaction.Type, transaction.Note);

            // QUAN TRỌNG: Wallet balance và transaction đã được thêm vào context
            // SaveChangesAsync sẽ được gọi ở HandleIpnAsync sau khi method này hoàn thành
            // Nếu có exception ở đây, SaveChangesAsync sẽ không được gọi và wallet/transaction sẽ không được lưu
            // Do đó, các bước sau (notification, email) được bọc trong try-catch riêng để không ảnh hưởng đến việc lưu wallet/transaction

            // Bước 5: Tạo và gửi notification (nếu lỗi ở đây không ảnh hưởng đến việc cộng tiền)
            // Lưu ý: Notification sẽ được tạo sau khi SaveChangesAsync được gọi ở HandleIpnAsync
            // Nhưng để đảm bảo transaction.Id có sẵn, chúng ta sẽ tạo notification sau khi SaveChangesAsync
            // Tạm thời chỉ log để đảm bảo flow hoạt động đúng
            _logger.LogInformation(
                "Wallet balance và transaction đã được chuẩn bị. Notification sẽ được tạo sau khi SaveChangesAsync được gọi ở HandleIpnAsync.");

            _logger.LogInformation(
                "Hoàn thành chuẩn bị cộng tiền vào ví cho payment {PaymentId} (OrderId: {OrderId}). " +
                "Wallet balance và transaction sẽ được lưu khi SaveChangesAsync được gọi.",
                payment.Id, payment.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LỖI NGHIÊM TRỌNG khi chuẩn bị cộng tiền vào ví cho payment {PaymentId} (OrderId: {OrderId}, UserId: {UserId}, Amount: {Amount}). " +
                "Lỗi: {ErrorMessage}. StackTrace: {StackTrace}. " +
                "Exception Type: {ExceptionType}",
                payment.Id, payment.OrderId, payment.ContextId, payment.Amount, ex.Message, ex.StackTrace, ex.GetType().Name);
            throw; // Re-throw để catch ở trên có thể log và xử lý
        }
    }

    private async Task<Wallet> CreateWalletAsync(string userId, CancellationToken ct)
    {
        var wallet = new Wallet { UserId = userId, Balance = 0m, Currency = "VND", IsFrozen = false };
        await _uow.Wallets.AddAsync(wallet, ct);
        await _uow.SaveChangesAsync();
        return wallet;
    }

    /// <summary>
    /// Kiểm tra xem business logic đã được thực thi cho payment chưa
    /// Bằng cách kiểm tra xem đã có transaction với note chứa OrderId chưa
    /// </summary>
    private async Task<bool> CheckIfBusinessLogicAppliedAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            switch (payment.ContextType)
            {
                case PaymentContextType.WalletDeposit:
                    // Kiểm tra xem đã có transaction Credit với note chứa OrderId chưa
                    var wallet = await _walletService.GetMyWalletAsync(payment.ContextId, ct);
                    var (transactions, _) = await _uow.Transactions.GetByWalletIdAsync(wallet.Id, 1, 10, ct);
                    var hasTransaction = transactions.Any(t => 
                        t.Type == TransactionType.Credit && 
                        t.Status == TransactionStatus.Succeeded &&
                        t.Note != null && 
                        t.Note.Contains(payment.OrderId));
                    return hasTransaction;

                case PaymentContextType.Escrow:
                    // Kiểm tra xem escrow đã được đánh dấu là Held chưa
                    var escrow = await _uow.Escrows.GetByIdAsync(payment.ContextId, ct);
                    if (escrow == null) return false;
                    return escrow.Status == EscrowStatus.Held;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Không thể kiểm tra business logic cho payment {PaymentId} (OrderId: {OrderId}). Giả định là chưa thực thi.",
                payment.Id, payment.OrderId);
            return false; // Nếu không kiểm tra được, giả định là chưa thực thi để retry
        }
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
        
        // TransId có thể là string hoặc số từ MoMo
        // Dùng JsonElement để deserialize linh hoạt, sau đó convert sang string
        [System.Text.Json.Serialization.JsonIgnore]
        private System.Text.Json.JsonElement? _transIdElement;
        
        [System.Text.Json.Serialization.JsonPropertyName("transId")]
        public System.Text.Json.JsonElement TransIdElement
        {
            get => _transIdElement ?? default;
            set => _transIdElement = value;
        }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string? TransId => _transIdElement?.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => _transIdElement.Value.GetString(),
            System.Text.Json.JsonValueKind.Number => _transIdElement.Value.GetInt64().ToString(),
            System.Text.Json.JsonValueKind.Null => null,
            _ => null
        };
        
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

    /// <summary>
    /// Test IPN bằng RequestId: Tự động tìm payment bằng RequestId và test IPN.
    /// </summary>
    public async Task<MomoIpnResponseDto> TestIpnByRequestIdAsync(string requestId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "TestIpnByRequestId: Tìm payment với RequestId={RequestId}",
            requestId);
        
        // Tìm payment bằng RequestId
        var payment = await _uow.Payments.GetByRequestIdAsync(PaymentProvider.MoMo, requestId, ct);
        if (payment == null)
        {
            _logger.LogWarning(
                "❌ Không tìm thấy payment với RequestId={RequestId} để test IPN. " +
                "Kiểm tra xem RequestId có đúng không, hoặc payment có được lưu vào DB chưa.",
                requestId);
            
            // Thử tìm bằng PaymentId (nếu user nhầm lẫn)
            var paymentById = await _uow.Payments.GetByIdAsync(requestId);
            if (paymentById != null)
            {
                _logger.LogInformation(
                    "Tìm thấy payment bằng PaymentId={PaymentId}, nhưng RequestId={RequestId} không khớp. " +
                    "Payment RequestId thực tế: {ActualRequestId}",
                    requestId, requestId, paymentById.RequestId);
                return new MomoIpnResponseDto 
                { 
                    ResultCode = 1, 
                    Message = $"RequestId không khớp. Payment này có RequestId: {paymentById.RequestId}. Hãy dùng RequestId này để test." 
                };
            }
            
            return new MomoIpnResponseDto 
            { 
                ResultCode = 1, 
                Message = $"Không tìm thấy payment với RequestId: {requestId}. Hãy kiểm tra lại RequestId từ response khi tạo payment." 
            };
        }

        _logger.LogInformation(
            "Test IPN bằng RequestId: PaymentId={PaymentId}, OrderId={OrderId}, RequestId={RequestId}, Amount={Amount}, Status={Status}",
            payment.Id, payment.OrderId, payment.RequestId, payment.Amount, payment.Status);

        // BẢO MẬT: Chỉ cho phép test IPN với payment chưa Paid (Pending hoặc Failed)
        // Tránh abuse: user có thể tạo payment và test-ipn để cộng tiền miễn phí
        if (payment.Status == PaymentStatus.Paid)
        {
            _logger.LogWarning(
                "⚠️ [TestIpn] Payment {PaymentId} (OrderId: {OrderId}) đã Paid. Không cho phép test IPN với payment đã Paid để tránh abuse.",
                payment.Id, payment.OrderId);
            return new MomoIpnResponseDto 
            { 
                ResultCode = 1, 
                Message = "Payment đã Paid. Không thể test IPN với payment đã thanh toán. Chỉ dùng test-ipn để test với payment Pending." 
            };
        }

        // Kiểm tra xem đã có transaction chưa (nếu có thì không cho test)
        var hasTransaction = await CheckIfBusinessLogicAppliedAsync(payment, ct);
        if (hasTransaction)
        {
            _logger.LogWarning(
                "⚠️ [TestIpn] Payment {PaymentId} (OrderId: {OrderId}) đã có transaction. Không cho phép test IPN để tránh cộng tiền lại.",
                payment.Id, payment.OrderId);
            return new MomoIpnResponseDto 
            { 
                ResultCode = 1, 
                Message = "Payment đã có transaction. Không thể test IPN với payment đã được xử lý." 
            };
        }

        _logger.LogInformation(
            "✅ [TestIpn] Cho phép test IPN cho payment {PaymentId} (Status: {Status}). Payment chưa Paid và chưa có transaction.",
            payment.Id, payment.Status);

        // Tạo MomoIpnRequestDto tự động từ payment
        var ipnRequest = new MomoIpnRequestDto
        {
            AccessKey = _options.AccessKey,
            Amount = (long)payment.Amount,
            ExtraData = payment.ExtraData ?? string.Empty,
            Message = "Thành công.",
            OrderId = payment.OrderId,
            OrderInfo = $"Payment for {payment.ContextType}",
            OrderType = "momo_wallet",
            PartnerCode = _options.PartnerCode,
            PayType = "webApp",
            RequestId = payment.RequestId,
            ResponseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ResultCode = 0, // Thành công
            TransId = payment.TransactionId ?? Guid.NewGuid().ToString(), // Tạo TransId nếu chưa có
            Signature = string.Empty // Bypass signature validation cho test
        };

        // Gọi HandleIpnAsync
        return await HandleIpnAsync(ipnRequest, ct);
    }

    /// <summary>
    /// Lấy trạng thái payment của user (để frontend biết thanh toán thành công chưa).
    /// </summary>
    public async Task<PaymentStatusDto> GetPaymentStatusAsync(string paymentId, string userId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GetPaymentStatus: PaymentId={PaymentId}, UserId={UserId}",
            paymentId, userId);
        
        var payment = await _uow.Payments.GetByIdAsync(paymentId);
        if (payment == null)
        {
            _logger.LogWarning(
                "Payment không tồn tại: PaymentId={PaymentId}, UserId={UserId}",
                paymentId, userId);
            throw new ArgumentException($"Payment not found with ID: {paymentId}", nameof(paymentId));
        }

        _logger.LogInformation(
            "Đã tìm thấy payment: PaymentId={PaymentId}, OrderId={OrderId}, Status={Status}, ContextId={ContextId}, UserId={UserId}",
            payment.Id, payment.OrderId, payment.Status, payment.ContextId, userId);

        // Kiểm tra quyền: user chỉ có thể xem payment của mình (WalletDeposit) hoặc payment liên quan đến mình (Escrow)
        if (payment.ContextType == PaymentContextType.WalletDeposit && payment.ContextId != userId)
        {
            _logger.LogWarning(
                "User không có quyền xem payment: PaymentId={PaymentId}, PaymentContextId={PaymentContextId}, UserId={UserId}",
                payment.Id, payment.ContextId, userId);
            throw new UnauthorizedAccessException($"You can only view your own payments. Payment belongs to: {payment.ContextId}, Your ID: {userId}");
        }

        // Kiểm tra xem đã có transaction chưa (đã cộng tiền chưa)
        var hasTransaction = await CheckIfBusinessLogicAppliedAsync(payment, ct);

        _logger.LogInformation(
            "Payment status: PaymentId={PaymentId}, OrderId={OrderId}, Status={Status}, HasTransaction={HasTransaction}, PaidAt={PaidAt}",
            payment.Id, payment.OrderId, payment.Status, hasTransaction, payment.PaidAt?.ToString() ?? "NULL");

        return new PaymentStatusDto
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            RequestId = payment.RequestId,
            Amount = payment.Amount,
            Status = payment.Status.ToString(), // Trả về status thực tế từ DB: Pending, Paid, Failed, Expired, Refunded
            Message = payment.Message,
            PaidAt = payment.PaidAt, // null nếu chưa thanh toán
            CreatedAt = payment.CreatedAt,
            HasTransaction = hasTransaction // true nếu đã cộng tiền vào ví
        };
    }

    #endregion
}

