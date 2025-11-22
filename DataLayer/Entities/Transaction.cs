using DataLayer.Enum;

namespace DataLayer.Entities
{
   
    public partial class Transaction : BaseEntity
    {
        public string? WalletId { get; set; }

        
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; }

        public decimal Amount { get; set; } 

        public string? Note { get; set; }
        public string? CounterpartyUserId { get; set; }

        public virtual Wallet? Wallet { get; set; }
    }
}
