namespace BusinessLayer.DTOs.Wallet;

public class WalletResponseDto
{
    public string Id { get; set; } = default!;
    public string? UserId { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsFrozen { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TransactionDto
{
    public string Id { get; set; } = default!;
    public string? WalletId { get; set; }
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DepositWithdrawDto
{
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class TransferDto
{
    public string ToUserId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}


