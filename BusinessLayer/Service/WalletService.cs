using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using DataLayer.Entities;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Service
{
    public class WalletService : IWalletService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notificationService;

        public WalletService(IUnitOfWork uow, INotificationService notificationService)
        {
            _uow = uow;
            _notificationService = notificationService;
        }

        // Controller gọi GetMyWalletAsync → alias sang GetOrCreate
        public async Task<Wallet> GetMyWalletAsync(string userId, CancellationToken ct = default)
        {
            var w = await _uow.Wallets.GetByUserIdAsync(userId, ct);
            if (w != null) return w;

            w = new Wallet { UserId = userId, Balance = 0m, Currency = "VND", IsFrozen = false };
            await _uow.Wallets.AddAsync(w, ct);
            await _uow.SaveChangesAsync();
            return w;
        }

        public async Task<(System.Collections.Generic.IEnumerable<Transaction> items, int total)>
            GetMyTransactionsAsync(string userId, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var wallet = await GetMyWalletAsync(userId, ct);
            var (items, total) = await _uow.Transactions.GetByWalletIdAsync(wallet.Id, pageNumber, pageSize, ct);
            return (items, total);
        }

        public async Task<OperationResult> DepositAsync(string userId, decimal amount, string? note, CancellationToken ct = default)
        {
            if (amount <= 0) return new OperationResult { Status = "Fail", Message = "Amount must be > 0" };

            using var tx = await _uow.BeginTransactionAsync();

            var wallet = await GetMyWalletAsync(userId, ct);
            if (wallet.IsFrozen) return new OperationResult { Status = "Fail", Message = "Wallet is frozen" };

            wallet.Balance += amount;
            
            // Attach wallet để EF track changes (vì GetByUserIdAsync dùng AsNoTracking)
            await _uow.Wallets.Update(wallet);

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Type = TransactionType.Credit,
                Status = TransactionStatus.Succeeded,
                Amount = amount,
                Note = note
            };
            await _uow.Transactions.AddAsync(transaction, ct);

            try
            {
                await _uow.SaveChangesAsync(); // Save transaction để có transaction.Id
                
                // Tạo notification sau khi save (để có transaction.Id)
                var notification = await _notificationService.CreateWalletNotificationAsync(
                    userId, 
                    NotificationType.WalletDeposit, 
                    amount, 
                    note, 
                    transaction.Id, 
                    ct);
                
                await _uow.SaveChangesAsync(); // Save notification
                
                // Gửi real-time notification sau khi save (notification.Id đã được set)
                await _notificationService.SendRealTimeNotificationAsync(userId, notification, ct);
                
                await tx.CommitAsync();
                return new OperationResult { Status = "Ok" };
            }
            catch (DbUpdateConcurrencyException)
            {
                return new OperationResult { Status = "Fail", Message = "Conflict, please retry" };
            }
        }

        public async Task<OperationResult> WithdrawAsync(string userId, decimal amount, string? note, CancellationToken ct = default)
        {
            if (amount <= 0) return new OperationResult { Status = "Fail", Message = "Amount must be > 0" };

            using var tx = await _uow.BeginTransactionAsync();

            var wallet = await GetMyWalletAsync(userId, ct);
            if (wallet.IsFrozen) return new OperationResult { Status = "Fail", Message = "Wallet is frozen" };
            if (wallet.Balance < amount) return new OperationResult { Status = "Fail", Message = "Insufficient balance" };

            wallet.Balance -= amount;
            
            // Attach wallet để EF track changes
            await _uow.Wallets.Update(wallet);

            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Type = TransactionType.Debit,
                Status = TransactionStatus.Succeeded,
                Amount = amount,
                Note = note
            };
            await _uow.Transactions.AddAsync(transaction, ct);

            try
            {
                await _uow.SaveChangesAsync(); // Save transaction để có transaction.Id
                
                // Tạo notification sau khi save
                var notification = await _notificationService.CreateWalletNotificationAsync(
                    userId, 
                    NotificationType.WalletWithdraw, 
                    amount, 
                    note, 
                    transaction.Id, 
                    ct);
                
                await _uow.SaveChangesAsync(); // Save notification
                
                // Gửi real-time notification sau khi save
                await _notificationService.SendRealTimeNotificationAsync(userId, notification, ct);
                
                await tx.CommitAsync();
                return new OperationResult { Status = "Ok" };
            }
            catch (DbUpdateConcurrencyException)
            {
                return new OperationResult { Status = "Fail", Message = "Conflict, please retry" };
            }
        }

        public async Task<OperationResult> TransferAsync(string fromUserId, string toUserId, decimal amount, string? note, CancellationToken ct = default)
        {
            if (fromUserId == toUserId) return new OperationResult { Status = "Fail", Message = "Cannot transfer to yourself" };
            if (amount <= 0) return new OperationResult { Status = "Fail", Message = "Amount must be > 0" };

            using var tx = await _uow.BeginTransactionAsync();

            var from = await GetMyWalletAsync(fromUserId, ct);
            var to = await GetMyWalletAsync(toUserId, ct);

            if (from.IsFrozen) return new OperationResult { Status = "Fail", Message = "Sender wallet is frozen" };
            if (to.IsFrozen) return new OperationResult { Status = "Fail", Message = "Receiver wallet is frozen" };
            if (from.Balance < amount) return new OperationResult { Status = "Fail", Message = "Insufficient balance" };

            from.Balance -= amount;
            to.Balance += amount;
            
            // Attach wallets để EF track changes
            await _uow.Wallets.Update(from);
            await _uow.Wallets.Update(to);

            var fromTransaction = new Transaction
            {
                WalletId = from.Id,
                Type = TransactionType.TransferOut,
                Status = TransactionStatus.Succeeded,
                Amount = amount,
                Note = note,
                CounterpartyUserId = toUserId
            };
            await _uow.Transactions.AddAsync(fromTransaction, ct);

            var toTransaction = new Transaction
            {
                WalletId = to.Id,
                Type = TransactionType.TransferIn,
                Status = TransactionStatus.Succeeded,
                Amount = amount,
                Note = note,
                CounterpartyUserId = fromUserId
            };
            await _uow.Transactions.AddAsync(toTransaction, ct);

            try
            {
                await _uow.SaveChangesAsync(); // Save transactions để có transaction.Id
                
                // Tạo notifications sau khi save
                var fromNotification = await _notificationService.CreateWalletNotificationAsync(
                    fromUserId, 
                    NotificationType.WalletTransferOut, 
                    amount, 
                    note, 
                    fromTransaction.Id, 
                    ct);
                
                var toNotification = await _notificationService.CreateWalletNotificationAsync(
                    toUserId, 
                    NotificationType.WalletTransferIn, 
                    amount, 
                    note, 
                    toTransaction.Id, 
                    ct);
                
                await _uow.SaveChangesAsync(); // Save notifications
                
                // Gửi real-time notifications sau khi save
                await _notificationService.SendRealTimeNotificationAsync(fromUserId, fromNotification, ct);
                await _notificationService.SendRealTimeNotificationAsync(toUserId, toNotification, ct);
                
                await tx.CommitAsync();
                return new OperationResult { Status = "Ok" };
            }
            catch (DbUpdateConcurrencyException)
            {
                return new OperationResult { Status = "Fail", Message = "Conflict, please retry" };
            }
        }

        public async Task<OperationResult> DepositForStudentAsync(string payerUserId, string studentUserId, decimal amount, string? note, CancellationToken ct = default)
        {
            if (amount <= 0) return new OperationResult { Status = "Fail", Message = "Amount must be > 0" };

            using var tx = await _uow.BeginTransactionAsync();

            var studentWallet = await GetMyWalletAsync(studentUserId, ct);
            if (studentWallet.IsFrozen) return new OperationResult { Status = "Fail", Message = "Student wallet is frozen" };

            studentWallet.Balance += amount;
            
            // Attach wallet để EF track changes
            await _uow.Wallets.Update(studentWallet);

            var transaction = new Transaction
            {
                WalletId = studentWallet.Id,
                Type = TransactionType.Credit,
                Status = TransactionStatus.Succeeded,
                Amount = amount,
                Note = string.IsNullOrWhiteSpace(note) ? $"Topup by parent {payerUserId}" : note,
                CounterpartyUserId = payerUserId
            };
            await _uow.Transactions.AddAsync(transaction, ct);

            try
            {
                await _uow.SaveChangesAsync(); // Save transaction để có transaction.Id
                
                // Tạo notification sau khi save
                var notification = await _notificationService.CreateWalletNotificationAsync(
                    studentUserId, 
                    NotificationType.WalletDeposit, 
                    amount, 
                    note, 
                    transaction.Id, 
                    ct);
                
                await _uow.SaveChangesAsync(); // Save notification
                
                // Gửi real-time notification sau khi save
                await _notificationService.SendRealTimeNotificationAsync(studentUserId, notification, ct);
                
                await tx.CommitAsync();
                return new OperationResult { Status = "Ok" };
            }
            catch (DbUpdateConcurrencyException)
            {
                return new OperationResult { Status = "Fail", Message = "Conflict, please retry" };
            }
        }
    }
}
