using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BusinessLayer.DTOs.Wallet;
using DataLayer.Entities;

namespace BusinessLayer.Service.Interface
{
    public interface IWalletService
    {
        // Khớp với controller: GET api/Wallet/me
        Task<Wallet> GetMyWalletAsync(string userId, CancellationToken ct = default);

        // Khớp với controller: GET api/Wallet/me/transactions
        Task<(IEnumerable<Transaction> items, int total)>
            GetMyTransactionsAsync(string userId, int pageNumber, int pageSize, CancellationToken ct = default);

        // Khớp với controller: POST deposit/withdraw/transfer (trả OperationResult)
        Task<OperationResult> DepositAsync(string userId, decimal amount, string? note, CancellationToken ct = default);
        Task<OperationResult> WithdrawAsync(string userId, decimal amount, string? note, CancellationToken ct = default);
        Task<OperationResult> TransferAsync(string fromUserId, string toUserId, decimal amount, string? note, CancellationToken ct = default);

        // (Tuỳ chọn) Parent nạp cho Student
        Task<OperationResult> DepositForStudentAsync(string payerUserId, string studentUserId, decimal amount, string? note, CancellationToken ct = default);
    }
}
