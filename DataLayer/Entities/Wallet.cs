using System.Collections.Generic;

namespace DataLayer.Entities
{
   
    public partial class Wallet : BaseEntity
    {
        public string? UserId { get; set; }

        public decimal Balance { get; set; } = 0m;

        
        public string Currency { get; set; } = "VND";
        public bool IsFrozen { get; set; } = false;
        public byte[] RowVersion { get; set; } = default!;

        public virtual User? User { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
