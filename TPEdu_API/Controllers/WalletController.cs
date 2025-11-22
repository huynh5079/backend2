using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;
using System.Linq;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/wallet")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyWallet()
        {
            var userId = User.RequireUserId();
            var wallet = await _walletService.GetMyWalletAsync(userId);
            var result = new WalletResponseDto
            {
                Id = wallet.Id,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                Currency = wallet.Currency,
                IsFrozen = wallet.IsFrozen,
                CreatedAt = wallet.CreatedAt,
                UpdatedAt = wallet.UpdatedAt
            };
            return Ok(result);
        }

        [HttpGet("me/transactions")]
        public async Task<IActionResult> GetMyTransactions([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
        {
            var userId = User.RequireUserId();
            var (items, total) = await _walletService.GetMyTransactionsAsync(userId, pageNumber, pageSize);
            var transactions = items.Select(t => new TransactionDto
            {
                Id = t.Id,
                WalletId = t.WalletId,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt
            });
            return Ok(new { items = transactions, page = pageNumber, size = pageSize, total });
        }

        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositWithdrawDto dto)
        {
            var userId = User.RequireUserId();
            var result = await _walletService.DepositAsync(userId, dto.Amount, dto.Note);
            if (result.Status == "Fail") return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] DepositWithdrawDto dto)
        {
            var userId = User.RequireUserId();
            var result = await _walletService.WithdrawAsync(userId, dto.Amount, dto.Note);
            if (result.Status == "Fail") return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferDto dto)
        {
            var userId = User.RequireUserId();
            var result = await _walletService.TransferAsync(userId, dto.ToUserId, dto.Amount, dto.Note);
            if (result.Status == "Fail") return BadRequest(result);
            return Ok(result);
        }
    }
}


