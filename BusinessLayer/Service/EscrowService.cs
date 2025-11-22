using System;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Service
{
    public class EscrowService : IEscrowService
    {
        private readonly IUnitOfWork _uow;
        private readonly SystemWalletOptions _systemWalletOptions;
        private readonly INotificationService _notificationService;
        
        public EscrowService(IUnitOfWork uow, IOptions<SystemWalletOptions> systemWalletOptions, INotificationService notificationService)
        {
            _uow = uow;
            _systemWalletOptions = systemWalletOptions.Value;
            _notificationService = notificationService;
        }

        private async Task<Wallet> GetOrCreateWalletAsync(string userId, CancellationToken ct)
        {
            var w = await _uow.Wallets.GetByUserIdAsync(userId, ct);
            if (w != null) return w;

            w = new Wallet { UserId = userId, Balance = 0m, Currency = "VND", IsFrozen = false };
            await _uow.Wallets.AddAsync(w, ct);
            await _uow.SaveChangesAsync();
            return w;
        }

        public async Task<OperationResult> PayEscrowAsync(string actorUserId, PayEscrowRequest req, CancellationToken ct = default)
        {
            if (req.GrossAmount <= 0) return new OperationResult { Status = "Fail", Message = "Số tiền không hợp lệ" };

            using var tx = await _uow.BeginTransactionAsync();

            // 1) Xác định TutorUserId từ ClassId trước khi động vào ví/tiền
            var resolvedTutorUserId = await ResolveTutorUserIdFromClassIdAsync(req.ClassId, ct);
            if (string.IsNullOrWhiteSpace(resolvedTutorUserId))
            {
                // Trường hợp phổ biến: client đã truyền ClassAssign.Id thay vì Class.Id
                return new OperationResult { Status = "Fail", Message = "Không thể xác định gia sư từ ClassId. Vui lòng kiểm tra ID lớp (phải là Class.Id, không phải ClassAssign.Id)." };
            }

            var payerUserId = string.IsNullOrWhiteSpace(req.PayerStudentUserId) ? actorUserId : req.PayerStudentUserId;
            // Lấy ví payer và ví admin (system) – đảm bảo tồn tại
            var payerWallet = await GetOrCreateWalletAsync(payerUserId, ct);
            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);

            if (payerWallet.IsFrozen) return new OperationResult { Status = "Fail", Message = "Ví bị khóa" };
            if (payerWallet.Balance < req.GrossAmount) return new OperationResult { Status = "Fail", Message = "Số dư không đủ" };

            payerWallet.Balance -= req.GrossAmount;
            adminWallet.Balance += req.GrossAmount;
            
            await _uow.Wallets.Update(payerWallet);
            await _uow.Wallets.Update(adminWallet);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = payerWallet.Id,
                Type = TransactionType.PayEscrow,
                Status = TransactionStatus.Succeeded,
                Amount = req.GrossAmount * -1,
                Note = $"Pay escrow for class {req.ClassId}"
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.EscrowIn,
                Status = TransactionStatus.Succeeded,
                Amount = req.GrossAmount,
                Note = $"Receive escrow for class {req.ClassId}",
                CounterpartyUserId = payerUserId
            }, ct);

            var esc = new Escrow
            {
                ClassId = req.ClassId,
                PayerUserId = payerUserId,
                TutorUserId = resolvedTutorUserId, // Điền ngay lúc thanh toán
                GrossAmount = req.GrossAmount,
                CommissionRate = req.CommissionRate,
                Status = EscrowStatus.Held
            };
            await _uow.Escrows.AddAsync(esc, ct);
            await _uow.SaveChangesAsync(); // Save escrow để có esc.Id

            // Tạo notification sau khi save
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                payerUserId, 
                NotificationType.EscrowPaid, 
                req.GrossAmount, 
                req.ClassId, 
                esc.Id, 
                ct);
            
            await _uow.SaveChangesAsync(); // Save notification
            
            // Gửi real-time notification sau khi save
            await _notificationService.SendRealTimeNotificationAsync(payerUserId, notification, ct);
            
            await tx.CommitAsync();
            return new OperationResult { Status = "Ok" };
        }

        // Helper: suy TutorUserId từ ClassId nếu escrow chưa có
        private async Task<string?> ResolveTutorUserIdFromClassIdAsync(string classId, CancellationToken ct)
        {
            var cls = await _uow.Classes.GetByIdAsync(classId);
            if (cls == null || string.IsNullOrWhiteSpace(cls.TutorId)) return null;
        
            var tutorProfile = await _uow.TutorProfiles.GetByIdAsync(cls.TutorId);
            return tutorProfile?.UserId;
        }

        public async Task<OperationResult> ReleaseAsync(string adminUserId, ReleaseEscrowRequest req, CancellationToken ct = default)
        {
            var esc = await _uow.Escrows.GetByIdAsync(req.EscrowId, ct);
            if (esc == null) return new OperationResult { Status = "Fail", Message = "Escrow không tồn tại" };
            if (esc.Status != EscrowStatus.Held) return new OperationResult { Status = "Fail", Message = "Escrow không ở trạng thái Held" };

            using var tx = await _uow.BeginTransactionAsync();

            // Dùng ví hệ thống – nơi giữ escrow trong Pay
            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);

            // Đảm bảo có TutorUserId, nếu chưa thì suy từ ClassId
            var tutorUserId = string.IsNullOrWhiteSpace(esc.TutorUserId)
                ? await ResolveTutorUserIdFromClassIdAsync(esc.ClassId, ct)
                : esc.TutorUserId;

            if (string.IsNullOrWhiteSpace(tutorUserId))
                return new OperationResult { Status = "Fail", Message = "Escrow chưa gắn tutor" };

            esc.TutorUserId = tutorUserId;

            var tutorWallet = await GetOrCreateWalletAsync(tutorUserId, ct);

            var commission = Math.Round(esc.GrossAmount * esc.CommissionRate, 2, MidpointRounding.AwayFromZero);
            var net = esc.GrossAmount - commission;

            if (adminWallet.Balance < net) return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ" };

            adminWallet.Balance -= net;
            tutorWallet.Balance += net;
            
            // Attach wallets để EF track changes
            await _uow.Wallets.Update(adminWallet);
            await _uow.Wallets.Update(tutorWallet);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.PayoutOut,
                Status = TransactionStatus.Succeeded,
                Amount = -net,
                Note = $"Release payout for escrow {esc.Id}",
                CounterpartyUserId = esc.TutorUserId
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = tutorWallet.Id,
                Type = TransactionType.PayoutIn,
                Status = TransactionStatus.Succeeded,
                Amount = net,
                Note = $"Payout received for escrow {esc.Id}",
                CounterpartyUserId = adminUserId
            }, ct);

            // Ghi sổ doanh thu (không đổi số dư)
            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.Commission,
                Status = TransactionStatus.Succeeded,
                Amount = commission,
                Note = $"Commission for escrow {esc.Id}",
                CounterpartyUserId = esc.PayerUserId
            }, ct);

            esc.Status = EscrowStatus.Released;
            esc.ReleasedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(); // Save escrow changes

            // Tạo notification sau khi save
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                tutorUserId, 
                NotificationType.PayoutReceived, 
                net, 
                esc.ClassId, 
                esc.Id, 
                ct);
            
            await _uow.SaveChangesAsync(); // Save notification
            
            // Gửi real-time notification sau khi save
            await _notificationService.SendRealTimeNotificationAsync(tutorUserId, notification, ct);
            
            await tx.CommitAsync();
            return new OperationResult { Status = "Ok" };
        }

        public async Task<OperationResult> RefundAsync(string adminUserId, RefundEscrowRequest req, CancellationToken ct = default)
        {
            var esc = await _uow.Escrows.GetByIdAsync(req.EscrowId, ct);
            if (esc == null) return new OperationResult { Status = "Fail", Message = "Escrow không tồn tại" };
            if (esc.Status != EscrowStatus.Held) return new OperationResult { Status = "Fail", Message = "Chỉ refund khi Held" };

            using var tx = await _uow.BeginTransactionAsync();

            // Rút từ ví hệ thống – nơi đang giữ escrow
            var adminWallet = await GetOrCreateWalletAsync(_systemWalletOptions.SystemWalletUserId, ct);
            var payerWallet = await GetOrCreateWalletAsync(esc.PayerUserId, ct);

            if (adminWallet.Balance < esc.GrossAmount) return new OperationResult { Status = "Fail", Message = "Số dư admin không đủ" };

            adminWallet.Balance -= esc.GrossAmount;
            payerWallet.Balance += esc.GrossAmount;
            
            // Attach wallets để EF track changes
            await _uow.Wallets.Update(adminWallet);
            await _uow.Wallets.Update(payerWallet);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = adminWallet.Id,
                Type = TransactionType.RefundOut,
                Status = TransactionStatus.Succeeded,
                Amount = -esc.GrossAmount,
                Note = $"Refund escrow {esc.Id}",
                CounterpartyUserId = esc.PayerUserId
            }, ct);

            await _uow.Transactions.AddAsync(new Transaction
            {
                WalletId = payerWallet.Id,
                Type = TransactionType.RefundIn,
                Status = TransactionStatus.Succeeded,
                Amount = esc.GrossAmount,
                Note = $"Refund received for escrow {esc.Id}",
                CounterpartyUserId = adminUserId
            }, ct);

            esc.Status = EscrowStatus.Refunded;
            esc.RefundedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(); // Save escrow changes

            // Tạo notification sau khi save
            var notification = await _notificationService.CreateEscrowNotificationAsync(
                esc.PayerUserId, 
                NotificationType.EscrowRefunded, 
                esc.GrossAmount, 
                esc.ClassId, 
                esc.Id, 
                ct);
            
            await _uow.SaveChangesAsync(); // Save notification
            
            // Gửi real-time notification sau khi save
            await _notificationService.SendRealTimeNotificationAsync(esc.PayerUserId, notification, ct);
            
            await tx.CommitAsync();
            return new OperationResult { Status = "Ok" };
        }
    }
}


